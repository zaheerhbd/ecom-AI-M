using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agent.Tests
{
    /// <summary>Tests for the JSON sent to OpenAI and for the gen_ai.* telemetry names.</summary>
    public class LlmAndTelemetryTests
    {
        private static OpenAiLlmClient NewClient()
        {
            LlmOptions options = new LlmOptions();
            options.Provider = "OpenAI";
            options.Model = "gpt-5.4-mini";
            return new OpenAiLlmClient(new HttpClient(), Options.Create(options), NullLogger<OpenAiLlmClient>.Instance);
        }

        [Fact]
        public void RequestBody_LeavesTemperatureOut_WhenItIsNull()
        {
            List<ChatMessage> messages = new List<ChatMessage> { ChatMessage.UserMessage("hi") };

            JsonObject withoutTemperature = NewClient().BuildRequestBody(new LlmRequest(messages, null, null));
            JsonObject withTemperature = NewClient().BuildRequestBody(new LlmRequest(messages, null, 0.2));

            Assert.False(withoutTemperature.ContainsKey("temperature"));
            Assert.Equal(0.2, (double)withTemperature["temperature"]);
            Assert.Equal("gpt-5.4-mini", (string)withoutTemperature["model"]);
            Assert.False(withoutTemperature.ContainsKey("tools")); // no tools, no "tools" key
        }

        [Fact]
        public async Task Spans_UseTheGenAiNames()
        {
            // Listen to the engine's spans for the length of this test.
            ConcurrentBag<Activity> finished = new ConcurrentBag<Activity>();
            using (ActivityListener listener = new ActivityListener())
            {
                listener.ShouldListenTo = source => source.Name == AgentTelemetry.Name;
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
                listener.ActivityStopped = activity => finished.Add(activity);
                ActivitySource.AddActivityListener(listener);

                FakeLlmClient fake = new FakeLlmClient();
                fake.EnqueueToolCall("echo", "{\"text\":\"x\"}");
                fake.EnqueueAnswer("done");
                ServiceProvider services = TestHost.Build(fake);
                AgentResult result = await TestHost.AskAsync(services, "telemetry-test", "echo x");

                // Keep only this test's spans (other tests may run at the same time).
                List<Activity> mine = new List<Activity>();
                foreach (Activity activity in finished)
                {
                    if (activity.TraceId.ToHexString() == result.TraceId)
                        mine.Add(activity);
                }

                Assert.Contains(mine, a => (string)a.GetTagItem(AgentTelemetry.OperationNameTag) == "invoke_agent"
                                           && (string)a.GetTagItem(AgentTelemetry.ConversationIdTag) == "telemetry-test");
                Assert.Contains(mine, a => (string)a.GetTagItem(AgentTelemetry.OperationNameTag) == "chat"
                                           && (string)a.GetTagItem(AgentTelemetry.RequestModelTag) == "fake-model"
                                           && a.GetTagItem(AgentTelemetry.InputTokensTag) != null);
                Assert.Contains(mine, a => (string)a.GetTagItem(AgentTelemetry.OperationNameTag) == "execute_tool"
                                           && (string)a.GetTagItem(AgentTelemetry.ToolNameTag) == "echo");
            }
        }
    }
}
