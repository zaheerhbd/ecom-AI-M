using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Evals;
using Agent.Llm;
using Agent.Runs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agent.Tests
{
    /// <summary>Tests for the graders, the eval runner (repeat and flaky) and the regression gate.</summary>
    public class EvalTests
    {
        /// <summary>Builds an AgentResult with one tool step, for grader tests.</summary>
        private static AgentResult ResultWithToolCall(string toolName, string argumentsJson, string answer)
        {
            AgentResult result = new AgentResult(Guid.NewGuid(), "s1");
            result.Answer = answer;
            AgentStep step = new AgentStep();
            step.Type = AgentStepTypes.Tool;
            step.Name = toolName;
            step.Input = argumentsJson;
            result.Steps.Add(step);
            result.ToolsUsed.Add(toolName);
            return result;
        }

        private static ExpectedToolCall Expect(string name, string argsJson)
        {
            ExpectedToolCall expected = new ExpectedToolCall();
            expected.Name = name;
            expected.Args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson);
            return expected;
        }

        [Fact]
        public void ArgumentsMatch_ComparesTextIgnoringCase_AndNumbersAsNumbers()
        {
            Dictionary<string, JsonElement> expected = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"type\":\"Hats\",\"maxPrice\":40}");

            Assert.True(Graders.ArgumentsMatch("{\"type\":\"hats\",\"maxPrice\":40.0,\"sort\":\"priceAsc\"}", expected));
            Assert.True(Graders.ArgumentsMatch("{\"TYPE\":\"Hats\",\"maxPrice\":\"40\"}", expected));
            Assert.False(Graders.ArgumentsMatch("{\"type\":\"Boots\",\"maxPrice\":40}", expected));
            Assert.False(Graders.ArgumentsMatch("{\"type\":\"Hats\"}", expected));
            Assert.False(Graders.ArgumentsMatch("{broken", expected));
        }

        [Fact]
        public void Grade_ChecksExpectedToolsWithArguments()
        {
            EvalCase evalCase = new EvalCase();
            evalCase.Id = "T1";
            evalCase.Input = "cheapest hat";
            evalCase.ExpectedTools = new List<ExpectedToolCall> { Expect("search_products", "{\"type\":\"Hats\"}") };

            List<EvalCheck> good = Graders.Grade(evalCase, ResultWithToolCall("search_products", "{\"type\":\"Hats\",\"sort\":\"priceAsc\"}", "The cheapest hat is $10."));
            List<EvalCheck> wrongArgs = Graders.Grade(evalCase, ResultWithToolCall("search_products", "{\"type\":\"Boots\"}", "Boots!"));

            Assert.True(Graders.ToolChecksPassed(good));
            Assert.False(Graders.ToolChecksPassed(wrongArgs));
        }

        [Fact]
        public void Grade_EmptyExpectedTools_MeansNoToolShouldBeCalled()
        {
            EvalCase evalCase = new EvalCase();
            evalCase.Id = "T2";
            evalCase.Input = "hello";
            evalCase.ExpectedTools = new List<ExpectedToolCall>();

            List<EvalCheck> checks = Graders.Grade(evalCase, ResultWithToolCall("echo", "{}", "hi"));

            Assert.False(Graders.ToolChecksPassed(checks));
        }

        [Fact]
        public void Grade_ClarifyAndDecline()
        {
            EvalCase clarify = new EvalCase();
            clarify.Id = "C";
            clarify.Input = "something warm";
            clarify.ShouldClarify = true;

            EvalCase decline = new EvalCase();
            decline.Id = "D";
            decline.Input = "do you sell laptops?";
            decline.ShouldDecline = true;

            AgentResult asksBack = new AgentResult(Guid.NewGuid(), "s");
            asksBack.Answer = "Do you mean a hat or gloves?";
            AgentResult saysNo = new AgentResult(Guid.NewGuid(), "s");
            saysNo.Answer = "Sorry, we don’t sell laptops.";
            AgentResult guesses = new AgentResult(Guid.NewGuid(), "s");
            guesses.Answer = "Here is a great laptop for $500.";

            Assert.True(AllPassed(Graders.Grade(clarify, asksBack)));
            Assert.True(AllPassed(Graders.Grade(decline, saysNo)));
            Assert.False(AllPassed(Graders.Grade(decline, guesses)));
        }

        [Fact]
        public async Task EvalRunner_RunsEachCaseNTimes_AndFlagsFlakyCases()
        {
            // The fake answers "yes", "no", "yes": the case passes 2 out of 3 times.
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueAnswer("yes");
            fake.EnqueueAnswer("no");
            fake.EnqueueAnswer("yes");
            fake.EnqueueAnswer("hello");
            ServiceProvider services = TestHost.Build(fake);

            EvalCase flaky = new EvalCase();
            flaky.Id = "FLAKY";
            flaky.Input = "say yes";
            flaky.MustContain = new List<string> { "yes" };

            EvalCase steady = new EvalCase();
            steady.Id = "STEADY";
            steady.Input = "say hello";
            steady.Repeat = 1;
            steady.MustContain = new List<string> { "hello" };

            EvalRunRequest request = new EvalRunRequest();
            request.Cases = new List<EvalCase> { flaky, steady };
            request.Repeat = 3;

            EvalRunner runner = services.GetRequiredService<EvalRunner>();
            EvalRunRecord run = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(EvalRunStatus.Completed, run.Status);
            Assert.Equal(4, run.TotalAttempts);
            Assert.Equal(3, run.PassedAttempts);
            Assert.Equal(0.75, run.PassRate);
            Assert.Equal(1, run.PassedCases);
            Assert.Equal(1, run.FlakyCases);
            Assert.NotNull(run.FinishedAt);

            List<EvalCaseResultRecord> results = await services.GetRequiredService<IEvalRunStore>().GetCaseResultsAsync(run.Id, CancellationToken.None);
            Assert.Equal(2, results[0].PassCount);
            Assert.Equal(3, results[0].RepeatCount);
            Assert.True(results[0].Flaky);
            Assert.False(results[0].Passed);
            Assert.Equal("no", results[0].Answer); // the failing attempt is the one shown

            // Every attempt was saved as an agent run marked as an eval.
            AgentRunRecord agentRun = await services.GetRequiredService<IAgentRunStore>().GetAsync(results[0].AgentRunId.Value, CancellationToken.None);
            Assert.Equal("eval", agentRun.Channel);
            Assert.Equal(run.Id, agentRun.EvalRunId);
        }

        [Fact]
        public async Task EvalRunner_RunsCasesAsTheSignedInUser()
        {
            FakeLlmClient fake = new FakeLlmClient();
            fake.EnqueueToolCall("get_my_orders", "{}");
            fake.EnqueueAnswer("You have orders.");
            ServiceProvider services = TestHost.Build(fake);

            EvalCase evalCase = new EvalCase();
            evalCase.Id = "ORD";
            evalCase.Input = "my orders";
            evalCase.SignedInAs = "anna@example.com";
            evalCase.ExpectedTools = new List<ExpectedToolCall> { Expect("get_my_orders", "{}") };

            EvalRunRequest request = new EvalRunRequest();
            request.Cases = new List<EvalCase> { evalCase };
            request.Repeat = 1;

            EvalRunRecord run = await services.GetRequiredService<EvalRunner>().RunAsync(request, CancellationToken.None);

            Assert.Equal(1, run.PassedCases);
            Assert.Equal(1.0, run.CorrectToolRate);
        }

        [Fact]
        public void Baseline_RegressionGate()
        {
            Assert.False(EvalBaseline.IsRegression(0.90, 0.86, 5)); // 4 points down: fine
            Assert.False(EvalBaseline.IsRegression(0.90, 0.85, 5)); // exactly 5 points down: fine
            Assert.True(EvalBaseline.IsRegression(0.90, 0.84, 5));  // 6 points down: regression
            Assert.False(EvalBaseline.IsRegression(0.80, 0.95, 5)); // better: fine
        }

        [Fact]
        public async Task CommandLine_SavesABaseline_ThenFailsOnARegression()
        {
            string folder = Path.Combine(Path.GetTempPath(), "agent-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string casesPath = Path.Combine(folder, "cases.json");
            string baselinePath = Path.Combine(folder, "baseline.json");
            File.WriteAllText(casesPath, "[ { \"id\": \"A\", \"input\": \"say yes\", \"mustContain\": [\"yes\"] } ]");

            try
            {
                // Run 1: the answer passes, so save it as the baseline.
                FakeLlmClient goodModel = new FakeLlmClient();
                goodModel.EnqueueAnswer("yes");
                ServiceProvider first = TestHost.Build(goodModel);
                int saveExit = await EvalCommandLine.RunAsync(first,
                    new[] { "--evals", "--repeat", "1", "--cases", casesPath, "--baseline", baselinePath, "--save-baseline" },
                    TextWriter.Null, CancellationToken.None);

                // Run 2: the answer now fails, so the pass rate drops from 100% to 0%.
                FakeLlmClient worseModel = new FakeLlmClient();
                worseModel.EnqueueAnswer("no");
                ServiceProvider second = TestHost.Build(worseModel);
                StringWriter output = new StringWriter();
                int gateExit = await EvalCommandLine.RunAsync(second,
                    new[] { "--evals", "--repeat", "1", "--cases", casesPath, "--baseline", baselinePath },
                    output, CancellationToken.None);

                Assert.Equal(0, saveExit);
                Assert.True(File.Exists(baselinePath));
                Assert.Equal(1, gateExit);
                Assert.Contains("REGRESSION", output.ToString());
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void CaseLoader_RejectsDuplicateIds()
        {
            string json = "[ { \"id\": \"A\", \"input\": \"x\" }, { \"id\": \"a\", \"input\": \"y\" } ]";

            Assert.Throws<InvalidDataException>(() => EvalCaseLoader.Parse(json));
        }

        [Fact]
        public void CaseLoader_ReadsTheDocumentedFormat()
        {
            string json =
                "[ { \"id\": \"MT-001\", \"category\": \"Multi-tool\", \"input\": \"cheapest hat and delivery?\",\n" +
                "    \"expectedTools\": [ { \"name\": \"search_products\", \"args\": { \"type\": \"Hats\", \"sort\": \"priceAsc\" } },\n" +
                "                         { \"name\": \"get_delivery_options\", \"args\": {} } ],\n" +
                "    \"mustNotContain\": [\"I don't know\"], \"shouldBlock\": false, \"signedInAs\": null, } ]";

            List<EvalCase> cases = EvalCaseLoader.Parse(json);

            Assert.Single(cases);
            Assert.Equal(2, cases[0].ExpectedTools.Count);
            Assert.Equal("Hats", cases[0].ExpectedTools[0].Args["type"].GetString());
            Assert.Empty(cases[0].MustContain);
        }

        [Fact]
        public void Statistics_MedianAndP95()
        {
            List<double> values = new List<double> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

            Assert.Equal(55, EvalStatistics.Median(values));
            Assert.Equal(100, EvalStatistics.Percentile(values, 95));
            Assert.Equal(0, EvalStatistics.Median(new List<double>()));
        }

        private static bool AllPassed(List<EvalCheck> checks)
        {
            foreach (EvalCheck check in checks)
            {
                if (!check.Passed)
                    return false;
            }

            return true;
        }
    }
}
