using System.Collections.Generic;
using Agent.Llm;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Agent.Tests
{
    /// <summary>Switching provider is one setting (Agent:Llm:Provider); each provider keeps its own key and model.</summary>
    public class ProviderSettingsTests
    {
        private static LlmOptions Resolve(Dictionary<string, string> values)
        {
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            IConfigurationSection llmSection = config.GetSection("Agent:Llm");
            LlmOptions llm = llmSection.Get<LlmOptions>() ?? new LlmOptions();
            ServiceCollectionExtensions.ApplyProviderDefaults(llm, llmSection, config);
            return llm;
        }

        private static Dictionary<string, string> BothKeys(string provider)
        {
            return new Dictionary<string, string>
            {
                { "Agent:Llm:Provider", provider },
                { "OpenAI:ApiKey", "openai-key" },
                { "OpenAI:ChatModel", "gpt-5.4-mini" },
                { "Gemini:ApiKey", "gemini-key" },
                { "Gemini:Model", "gemini-2.5-flash" },
            };
        }

        [Fact]
        public void Gemini_UsesGeminiKeyModelAndAddress()
        {
            LlmOptions llm = Resolve(BothKeys("Gemini"));

            Assert.True(llm.IsGemini);
            Assert.Equal("gemini-key", llm.ApiKey);
            Assert.Equal("gemini-2.5-flash", llm.Model);
            Assert.Equal(LlmOptions.GeminiBaseUrl, llm.BaseUrl);
        }

        [Fact]
        public void OpenAI_UsesOpenAiKeyModelAndAddress()
        {
            LlmOptions llm = Resolve(BothKeys("OpenAI"));

            Assert.Equal("openai-key", llm.ApiKey);
            Assert.Equal("gpt-5.4-mini", llm.Model);
            Assert.Equal(LlmOptions.OpenAiBaseUrl, llm.BaseUrl);
        }

        [Fact]
        public void ValuesInAgentLlm_StillWin()
        {
            Dictionary<string, string> values = BothKeys("Gemini");
            values["Agent:Llm:Model"] = "gemini-2.5-pro";
            values["Agent:Llm:ApiKey"] = "own-key";

            LlmOptions llm = Resolve(values);

            Assert.Equal("gemini-2.5-pro", llm.Model);
            Assert.Equal("own-key", llm.ApiKey);
        }

        [Fact]
        public void ToolCall_ExtraContent_IsSentBackUnchanged()
        {
            LlmOptions options = new LlmOptions();
            options.Provider = "Gemini";
            OpenAiLlmClient client = new OpenAiLlmClient(new System.Net.Http.HttpClient(),
                Microsoft.Extensions.Options.Options.Create(options),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiLlmClient>.Instance);

            ToolCall withSignature = new ToolCall("c1", "search_products", "{}", "{\"google\":{\"thought_signature\":\"abc\"}}");
            ToolCall withoutSignature = new ToolCall("c2", "search_products", "{}");
            List<ChatMessage> messages = new List<ChatMessage>
            {
                ChatMessage.UserMessage("hi"),
                ChatMessage.AssistantToolCalls(new List<ToolCall> { withSignature, withoutSignature }),
            };

            System.Text.Json.Nodes.JsonObject body = client.BuildRequestBody(new LlmRequest(messages, null, null));
            System.Text.Json.Nodes.JsonNode calls = body["messages"][1]["tool_calls"];

            Assert.Equal("abc", (string)calls[0]["extra_content"]["google"]["thought_signature"]);
            Assert.Null(calls[1]["extra_content"]);
        }

        [Fact]
        public void Gemini_WithoutAModelSetting_UsesTheDefaultModel()
        {
            LlmOptions llm = Resolve(new Dictionary<string, string> { { "Agent:Llm:Provider", "gemini" } });

            Assert.True(llm.IsGemini);
            Assert.Equal(LlmOptions.DefaultGeminiModel, llm.Model);
            Assert.Null(llm.ApiKey);
        }
    }
}
