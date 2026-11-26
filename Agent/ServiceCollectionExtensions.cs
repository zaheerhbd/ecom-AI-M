using System;
using System.Collections.Generic;
using Agent.Evals;
using Agent.Guardrails;
using Agent.Llm;
using Agent.Memory;
using Agent.Observability;
using Agent.Runs;
using Agent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Agent
{
    /// <summary>
    /// One place that wires the engine together with Dependency Injection (DI).
    ///
    /// LIFETIMES (how long each object lives) matter here:
    ///  - SINGLETON (one for the whole app): settings, guardrails, the system prompt, conversation
    ///    memory, the fake LLM, the eval runner. They hold no per-request data.
    ///  - SCOPED (one per HTTP request): AgentRunner, ToolRegistry, every tool and AgentRequestContext.
    ///    Shop tools use the database context, which is scoped and not thread-safe.
    ///  - The real LLM client comes from IHttpClientFactory, which manages its connections for us.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>The configuration section the engine reads ("Agent").</summary>
        public const string SectionName = "Agent";

        /// <summary>
        /// Registers the engine. Tools are added on the returned builder with AddTool&lt;T&gt;().
        /// Reads the "Agent" section; the LLM settings are in "Agent:Llm".
        /// </summary>
        public static AgentBuilder AddAgent(this IServiceCollection services, IConfiguration configuration)
        {
            IConfigurationSection section = configuration.GetSection(SectionName);
            IConfigurationSection llmSection = section.GetSection("Llm");

            // Agent:Engine picks the loop (Phase 7). A typo must stop the app at startup, not on the first chat.
            string engine = section["Engine"];
            if (!AgentEngines.IsCustom(engine) && !AgentEngines.IsAgentFramework(engine))
                throw new InvalidOperationException("Agent:Engine must be \"" + AgentEngines.Custom + "\" or \"" + AgentEngines.AgentFramework
                    + "\", but it is \"" + engine + "\".");

            // ---- Settings ----
            services.Configure<AgentOptions>(section);
            services.PostConfigure<AgentOptions>(EnsureSafeDefaults);
            services.Configure<LlmOptions>(llmSection);

            // Fill in the key, address and model for the chosen provider (see ApplyProviderDefaults).
            services.PostConfigure<LlmOptions>(llm => ApplyProviderDefaults(llm, llmSection, configuration));

            AddLlmClient(services, llmSection, configuration);
            AddGuardrails(services);

            // ---- Singletons ----
            // TryAdd: if the host already registered its own version, keep that one.
            services.TryAddSingleton<ContentPathResolver>();
            services.TryAddSingleton<SystemPromptProvider>();
            services.TryAddSingleton<IConversationStore, InMemoryConversationStore>();
            services.TryAddSingleton<IAgentRunStore, InMemoryAgentRunStore>();
            services.TryAddSingleton<IEvalRunStore, InMemoryEvalRunStore>();
            services.TryAddSingleton<MetricsCollector>();
            services.TryAddSingleton<TraceLogger>();
            services.TryAddSingleton<EvalRunner>();

            // ---- Scoped: one per request ----
            services.TryAddScoped<AgentRequestContext>();
            services.TryAddScoped(provider => new ToolRegistry(provider.GetServices<ITool>(),
                TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<AgentOptions>>().Value.ToolTimeoutSeconds)));
            services.TryAddScoped<AgentRunner>();

            return new AgentBuilder(services);
        }

        /// <summary>Fills in settings that must never be left empty.</summary>
        private static void EnsureSafeDefaults(AgentOptions options)
        {
            // A random canary per app start, unless config sets one. Each environment gets its own.
            if (string.IsNullOrWhiteSpace(options.Canary))
                options.Canary = "CANARY-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();

            if (options.Pricing == null)
                options.Pricing = new AgentPricing();

            if (options.Evals == null)
                options.Evals = new AgentEvalOptions();

            // Limits below 1 would stop the agent from ever answering.
            if (options.MaxSteps < 1)
                options.MaxSteps = 1;
            if (options.MaxHistoryMessages < 2)
                options.MaxHistoryMessages = 2;
            if (options.MaxToolCallsPerRun < 1)
                options.MaxToolCallsPerRun = 1;
            if (options.ToolTimeoutSeconds < 1)
                options.ToolTimeoutSeconds = 1;
        }

        /// <summary>LLM: fake or real, chosen by Agent:Llm:Provider. Fails at startup if the settings can't work.</summary>
        private static void AddLlmClient(IServiceCollection services, IConfigurationSection llmSection, IConfiguration configuration)
        {
            LlmOptions llm = llmSection.Get<LlmOptions>() ?? new LlmOptions();
            ApplyProviderDefaults(llm, llmSection, configuration);

            if (llm.IsFake)
            {
                services.TryAddSingleton<ILlmClient, FakeLlmClient>();
                return;
            }

            // Fail at startup (not on the first chat message) if settings are missing.
            if (llm.IsAzure && (string.IsNullOrWhiteSpace(llm.Endpoint) || string.IsNullOrWhiteSpace(llm.Deployment)))
                throw new InvalidOperationException("AzureOpenAI needs Agent:Llm:Endpoint and Agent:Llm:Deployment.");

            bool hasKey = !string.IsNullOrWhiteSpace(llm.ApiKey);
            if (llm.IsGemini && !hasKey)
                throw new InvalidOperationException("The agent uses Gemini but there is no API key. Set Gemini:ApiKey (free key from https://aistudio.google.com/apikey), or set Agent:Llm:Provider to \"OpenAI\".");

            bool usesOpenAiCloud = !llm.IsAzure && (llm.BaseUrl ?? "").IndexOf("api.openai.com", StringComparison.OrdinalIgnoreCase) >= 0;
            if (usesOpenAiCloud && !hasKey)
                throw new InvalidOperationException("The agent uses OpenAI but there is no API key. Set OpenAI:ApiKey (or Agent:Llm:ApiKey) in user-secrets.");

            // IHttpClientFactory reuses connections properly (avoids running out of sockets under load).
            services.AddHttpClient<OpenAiLlmClient>(http => http.Timeout = TimeSpan.FromSeconds(llm.TimeoutSeconds));
            services.TryAddTransient<ILlmClient>(provider => provider.GetRequiredService<OpenAiLlmClient>());
        }

        /// <summary>
        /// Each provider keeps its own key (and model) in its own section, so switching provider
        /// is ONE setting: Agent:Llm:Provider. Anything set directly in Agent:Llm still wins.
        ///  - Gemini: key from Gemini:ApiKey, model from Gemini:Model, Gemini's address.
        ///  - OpenAI: key from OpenAI:ApiKey, model from OpenAI:ChatModel.
        /// </summary>
        internal static void ApplyProviderDefaults(LlmOptions llm, IConfigurationSection llmSection, IConfiguration configuration)
        {
            bool modelSet = !string.IsNullOrWhiteSpace(llmSection["Model"]);
            bool baseUrlSet = !string.IsNullOrWhiteSpace(llmSection["BaseUrl"]);

            if (llm.IsGemini)
            {
                if (string.IsNullOrWhiteSpace(llm.ApiKey))
                    llm.ApiKey = configuration["Gemini:ApiKey"];
                if (!modelSet)
                    llm.Model = FirstNonEmpty(configuration["Gemini:Model"], LlmOptions.DefaultGeminiModel);
                if (!baseUrlSet)
                    llm.BaseUrl = FirstNonEmpty(configuration["Gemini:BaseUrl"], LlmOptions.GeminiBaseUrl);
                return;
            }

            if (string.IsNullOrWhiteSpace(llm.ApiKey))
                llm.ApiKey = configuration["OpenAI:ApiKey"];
            if (!modelSet && !llm.IsAzure)
                llm.Model = FirstNonEmpty(configuration["OpenAI:ChatModel"], LlmOptions.DefaultOpenAiModel);
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        /// <summary>Guardrails: order matters, they run top to bottom.</summary>
        private static void AddGuardrails(IServiceCollection services)
        {
            services.TryAddSingleton(provider =>
            {
                AgentOptions options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

                GuardrailPipeline input = new GuardrailPipeline(GuardrailStages.Input, new List<IGuardrail>
                {
                    new InputLengthGuardrail(options.MaxInputChars),
                    new PromptInjectionGuardrail(),
                    new PiiRedactionGuardrail(),
                });

                GuardrailPipeline output = new GuardrailPipeline(GuardrailStages.Output, new List<IGuardrail>
                {
                    new SecretLeakGuardrail(options.Canary),
                    new PiiRedactionGuardrail(),
                });

                return new AgentGuardrails(input, output);
            });
        }
    }
}
