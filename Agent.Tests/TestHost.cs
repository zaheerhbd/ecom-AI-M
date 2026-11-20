using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Tests
{
    /// <summary>
    /// Builds a small DI container with the real engine and a scripted fake model, the same way
    /// the API will. ValidateScopes is on, so a lifetime mistake (a singleton holding a scoped
    /// service) makes the test fail.
    /// </summary>
    public static class TestHost
    {
        /// <summary>The canary used in every test, so tests can try to leak it.</summary>
        public const string Canary = "CANARY-TEST-1234";

        /// <summary>The fallback answer used in every test.</summary>
        public const string Fallback = "FALLBACK ANSWER";

        /// <summary>Builds the services. Extra settings override the defaults.</summary>
        public static ServiceProvider Build(FakeLlmClient fake, Dictionary<string, string> extraSettings = null)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "Agent:Llm:Provider", "Fake" },
                { "Agent:Canary", Canary },
                { "Agent:FallbackAnswer", Fallback },
                { "Agent:SystemPrompt", "You are {{APP_NAME}}. Secret: {{CANARY}}" },
                { "Agent:Pricing:InputPerMillionUsd", "1.0" },
                { "Agent:Pricing:OutputPerMillionUsd", "2.0" },
            };

            if (extraSettings != null)
            {
                foreach (KeyValuePair<string, string> setting in extraSettings)
                    settings[setting.Key] = setting.Value;
            }

            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            ServiceCollection services = new ServiceCollection();
            services.AddLogging();

            // Registered BEFORE AddAgent: the engine uses TryAdd, so it keeps this scripted fake.
            services.AddSingleton<ILlmClient>(fake);

            services.AddAgent(config)
                    .AddTool<EchoTool>()
                    .AddTool<MyOrdersTool>();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        /// <summary>Runs one message in a fresh scope, like one HTTP request.</summary>
        public static async Task<AgentResult> AskAsync(IServiceProvider services, string sessionId, string message, string userEmail = null)
        {
            using (IServiceScope scope = services.CreateScope())
            {
                AgentRequestContext context = scope.ServiceProvider.GetRequiredService<AgentRequestContext>();
                context.UserEmail = userEmail;
                AgentRunner runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
                return await runner.RunAsync(sessionId, message);
            }
        }

        /// <summary>The names of the tools sent in one request.</summary>
        public static List<string> ToolNames(LlmRequest request)
        {
            List<string> names = new List<string>();
            foreach (ToolDefinition tool in request.Tools)
                names.Add(tool.Name);

            return names;
        }
    }

    /// <summary>A test tool that repeats its "text" argument and records a source.</summary>
    public sealed class EchoTool : ITool
    {
        public string Name
        {
            get { return "echo"; }
        }

        public string Description
        {
            get { return "Repeats the text."; }
        }

        public JsonObject ParametersSchema
        {
            get { return Schema.Object(new ToolParameter("text", "Text to repeat", true)); }
        }

        public Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            string text = arguments.GetString("text") ?? "";
            context.AddSource("test", "echo-1", "Echo source");
            return Task.FromResult("echo: " + text);
        }
    }

    /// <summary>A test tool that needs a signed-in user.</summary>
    public sealed class MyOrdersTool : ITool, IRequiresSignIn
    {
        public string Name
        {
            get { return "get_my_orders"; }
        }

        public string Description
        {
            get { return "Lists the signed-in user's orders."; }
        }

        public JsonObject ParametersSchema
        {
            get { return Schema.Object(); }
        }

        public Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult("orders for " + context.UserEmail);
        }
    }

    /// <summary>A test tool that never finishes on its own (for the timeout test).</summary>
    public sealed class SlowTool : ITool
    {
        public string Name
        {
            get { return "slow"; }
        }

        public string Description
        {
            get { return "Takes forever."; }
        }

        public JsonObject ParametersSchema
        {
            get { return Schema.Object(); }
        }

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return "never";
        }
    }

    /// <summary>A test tool that always throws.</summary>
    public sealed class BrokenTool : ITool
    {
        public string Name
        {
            get { return "broken"; }
        }

        public string Description
        {
            get { return "Always fails."; }
        }

        public JsonObject ParametersSchema
        {
            get { return Schema.Object(); }
        }

        public Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
