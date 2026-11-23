using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent.Evals;
using Agent.Llm;
using Agent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agent.Tests
{
    /// <summary>Phase 5: loop control (R11), shrinking old tool results (R12), groundedness and multi-turn evals.</summary>
    public class LoopControlAndMemoryTests
    {
        // ------------------------------------------------------------------ loop control

        [Fact]
        public async Task RepeatedCall_IsNotRunTwice_AndIsMarkedAsRepeat()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("echo", "{\"text\":\"hi\"}");
            fake.EnqueueToolCall("echo", "{ \"text\" : \"HI\" }"); // same call, different spacing and case
            fake.EnqueueAnswer("Done.");
            ServiceProvider services = TestHost.Build(fake);

            AgentResult result = await TestHost.AskAsync(services, "s1", "echo hi twice");

            List<AgentStep> tools = ToolSteps(result);
            Assert.Equal(2, tools.Count);
            Assert.False(tools[0].IsRepeat);
            Assert.True(tools[1].IsRepeat);
            Assert.Equal(new List<string> { "echo" }, result.ToolsUsed); // ran once
            Assert.Equal("Done.", result.Answer);

            // The model was told to use the earlier result.
            IReadOnlyList<ChatMessage> sent = fake.Requests[2].Messages;
            Assert.Contains("already called echo", sent[sent.Count - 1].Content);
        }

        [Fact]
        public async Task DifferentArguments_AreNotARepeat()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("echo", "{\"text\":\"a\"}");
            fake.EnqueueToolCall("echo", "{\"text\":\"b\"}");
            fake.EnqueueAnswer("Done.");
            ServiceProvider services = TestHost.Build(fake);

            AgentResult result = await TestHost.AskAsync(services, "s1", "echo a then b");

            Assert.Equal(2, result.ToolsUsed.Count);
            Assert.All(ToolSteps(result), s => Assert.False(s.IsRepeat));
        }

        [Fact]
        public void ToolCallKey_IgnoresKeyOrderAndCase()
        {
            string a = AgentRunner.ToolCallKey(new ToolCall("1", "search", "{\"type\":\"Hats\",\"maxPrice\":30}"));
            string b = AgentRunner.ToolCallKey(new ToolCall("2", "Search", "{\"maxPrice\":30, \"type\":\"hats\"}"));
            string c = AgentRunner.ToolCallKey(new ToolCall("3", "search", "{\"maxPrice\":31,\"type\":\"Hats\"}"));

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public async Task MaxToolCallsPerRun_StopsTheLoop_AndAnswersEveryCall()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("echo", "{\"text\":\"1\"}");
            fake.EnqueueToolCall("echo", "{\"text\":\"2\"}");
            fake.EnqueueToolCall("echo", "{\"text\":\"3\"}");
            ServiceProvider services = TestHost.Build(fake, new Dictionary<string, string> { { "Agent:MaxToolCallsPerRun", "2" } });

            AgentResult result = await TestHost.AskAsync(services, "s1", "echo three times");

            Assert.Equal(AgentStopReasons.MaxToolCalls, result.StopReason);
            Assert.Equal(2, result.ToolsUsed.Count);
            List<AgentStep> tools = ToolSteps(result);
            Assert.Equal(3, tools.Count);
            Assert.True(tools[2].IsError);
            Assert.StartsWith("Not run", tools[2].Output);
            Assert.Equal(3, fake.Requests.Count); // no 4th LLM call: the loop stopped

            // The next message still works: the saved history has a result for every tool request.
            fake.EnqueueAnswer("Hello again.");
            AgentResult next = await TestHost.AskAsync(services, "s1", "hi");
            Assert.Equal("Hello again.", next.Answer);
        }

        [Fact]
        public void ToolTimeoutSeconds_IsPassedToTheRegistry()
        {
            ServiceProvider services = TestHost.Build(new FakeLlmClient(), new Dictionary<string, string> { { "Agent:ToolTimeoutSeconds", "3" } });
            using (IServiceScope scope = services.CreateScope())
            {
                ToolRegistry registry = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
                Assert.Equal(TimeSpan.FromSeconds(3), registry.Timeout);
            }
        }

        // ------------------------------------------------------------------ shrinking old tool results

        [Fact]
        public async Task OldToolResults_AreShrunkToStubsWithIds_ForTheNextQuestion()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("long_list", "{}");
            fake.EnqueueAnswer("Here are 3 products.");
            fake.EnqueueAnswer("The second one.");
            ServiceProvider services = TestHost.Build(fake, null, b => b.AddTool<LongListTool>());

            await TestHost.AskAsync(services, "s1", "list products");

            // The CURRENT answer saw the full result.
            Assert.Contains(LongListTool.Marker, LastToolMessage(fake.Requests[1].Messages).Content);

            await TestHost.AskAsync(services, "s1", "which one is best?");

            // The NEXT question gets only the stub, which keeps the ids.
            string stub = LastToolMessage(fake.Requests[2].Messages).Content;
            Assert.DoesNotContain(LongListTool.Marker, stub);
            Assert.StartsWith("[long_list: 3 items, ids 11, 22, 33", stub);
            Assert.True(fake.Requests[2].Messages.Count > 0);
        }

        [Fact]
        public async Task ShrinkOldToolResults_False_KeepsTheFullResult()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("long_list", "{}");
            fake.EnqueueAnswer("Here are 3 products.");
            fake.EnqueueAnswer("The second one.");
            ServiceProvider services = TestHost.Build(fake, new Dictionary<string, string> { { "Agent:ShrinkOldToolResults", "false" } },
                b => b.AddTool<LongListTool>());

            await TestHost.AskAsync(services, "s1", "list products");
            await TestHost.AskAsync(services, "s1", "which one is best?");

            Assert.Contains(LongListTool.Marker, LastToolMessage(fake.Requests[2].Messages).Content);
        }

        [Fact]
        public void DefaultStub_ForPlainText_KeepsTheFirst200Characters()
        {
            string text = new string('a', 500);
            string stub = ToolResultSummariser.Summarise(null, "search_knowledge", text);

            Assert.StartsWith("[search_knowledge, shortened: aaaa", stub);
            Assert.True(stub.Length < 260);
            Assert.Equal("short", ToolResultSummariser.Summarise(null, "x", "short"));
        }

        // ------------------------------------------------------------------ groundedness

        [Fact]
        public void Groundedness_MatchesNumbersAsNumbers_AndFindsMadeUpPrices()
        {
            GroundednessResult check = Groundedness.Check(
                "The Alpine glove is $12 and the Storm glove is $19.99. Only 3 left!",
                new[] { "{\"items\":[{\"price\":12.00},{\"price\":24.50,\"stock\":3}]}" });

            Assert.Equal(2, check.GroundedCount);        // $12 (= 12.00) and "3 left"
            Assert.Equal(new List<string> { "$19.99" }, check.MadeUp);
        }

        [Fact]
        public void Groundedness_FindsAllKindsOfFacts()
        {
            List<KeyValuePair<string, decimal>> facts = Groundedness.ExtractFacts("Costs $1,200.50 or 40 USD. In stock: 7. 5 units ready.");

            List<decimal> values = new List<decimal>();
            foreach (KeyValuePair<string, decimal> fact in facts)
                values.Add(fact.Value);
            Assert.Equal(new List<decimal> { 1200.50m, 40m, 5m, 7m }, values);
        }

        [Fact]
        public async Task Runner_RecordsMadeUpFacts_ButAcceptsNumbersFromTheUser()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("echo", "{\"text\":\"price 15\"}");
            fake.EnqueueAnswer("Under your $50 budget: one item at $15, another at $99.");
            ServiceProvider services = TestHost.Build(fake);

            AgentResult result = await TestHost.AskAsync(services, "s1", "anything under $50?");

            Assert.Equal(2, result.GroundedFactCount);   // $50 (the user said it) and $15 (the tool said it)
            Assert.Equal(new List<string> { "$99" }, result.MadeUpFacts);
        }

        [Fact]
        public void Grader_AddsAGroundednessCheck_OnlyWhenThereAreFacts()
        {
            EvalCase evalCase = new EvalCase();
            evalCase.Id = "G";
            evalCase.Input = "x";

            AgentResult noFacts = new AgentResult(Guid.NewGuid(), "s");
            Assert.DoesNotContain(Graders.Grade(evalCase, noFacts), c => c.Name == Graders.GroundedCheckName);

            AgentResult madeUp = new AgentResult(Guid.NewGuid(), "s");
            madeUp.GroundedFactCount = 1;
            madeUp.MadeUpFacts.Add("$9");
            EvalCheck check = Graders.Grade(evalCase, madeUp).Find(c => c.Name == Graders.GroundedCheckName);
            Assert.False(check.Passed);
            Assert.Contains("$9", check.Detail);
        }

        // ------------------------------------------------------------------ multi-turn evals

        [Fact]
        public async Task MultiTurnCase_RunsInOneSession_AndGradesTheLastAnswer()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueAnswer("Your last order is #1.");
            fake.EnqueueAnswer("It arrives in 2-5 days.");
            ServiceProvider services = TestHost.Build(fake);

            List<EvalCase> cases = EvalCaseLoader.Parse(
                "[{ \"id\": \"MM\", \"turns\": [\"Where's my last order?\", \"When will it arrive?\"], " +
                "\"mustContain\": [\"2-5\"], \"maxToolCalls\": 0 }]");
            Assert.Equal("Where's my last order? -> When will it arrive?", cases[0].Input);

            EvalRunRequest request = new EvalRunRequest();
            request.Cases = cases;
            request.Repeat = 1;
            EvalRunRecord run = await services.GetRequiredService<EvalRunner>().RunAsync(request, CancellationToken.None);

            Assert.Equal(1, run.PassedAttempts);
            Assert.Equal(2, fake.Requests.Count);
            // The second turn saw the first one: system, user 1, answer 1, user 2.
            Assert.Equal(4, fake.Requests[1].Messages.Count);
        }

        [Fact]
        public void Loader_RejectsACaseWithNeitherInputNorTurns()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => EvalCaseLoader.Parse("[{ \"id\": \"X\" }]"));
            Assert.Throws<System.IO.InvalidDataException>(() => EvalCaseLoader.Parse("[{ \"id\": \"X\", \"turns\": [\"hi\", \" \"] }]"));
        }

        // ------------------------------------------------------------------ helpers

        private static List<AgentStep> ToolSteps(AgentResult result)
        {
            return result.Steps.FindAll(s => s.Type == AgentStepTypes.Tool);
        }

        private static ChatMessage LastToolMessage(IReadOnlyList<ChatMessage> messages)
        {
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Role == ChatRole.Tool)
                    return messages[i];
            }

            throw new InvalidOperationException("No tool message was sent.");
        }
    }

    /// <summary>A test tool that returns a long JSON list with ids (to test shrinking).</summary>
    public sealed class LongListTool : ITool
    {
        public const string Marker = "VERY-LONG-DESCRIPTION";

        public string Name
        {
            get { return "long_list"; }
        }

        public string Description
        {
            get { return "Returns a long list."; }
        }

        public JsonObject ParametersSchema
        {
            get { return Schema.Object(); }
        }

        public Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            string padding = Marker + new string('x', 200);
            return Task.FromResult("{\"total\":3,\"items\":[{\"id\":11,\"d\":\"" + padding + "\"},{\"id\":22,\"d\":\"" + padding + "\"},{\"id\":33,\"d\":\"" + padding + "\"}]}");
        }
    }
}
