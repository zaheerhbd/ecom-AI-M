using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agent.Evals
{
    /// <summary>
    /// THE REGRESSION GATE, run from the command line before merging a change to the prompt,
    /// tools or engine:
    ///
    ///   dotnet run --evals                                    run every case (repeat 3) and print a summary
    ///   dotnet run --evals --repeat 5                         run each case 5 times
    ///   dotnet run --evals --baseline Agent/evals/baseline.json   also compare with the saved baseline
    ///   dotnet run --evals --save-baseline                    accept this result as the new baseline
    ///   dotnet run --evals --judge                            add the LLM judge (costs extra)
    ///   dotnet run --evals --cases other-cases.json           use a different cases file
    ///
    /// The exit code is 1 when the run failed, the baseline is missing, or the pass rate dropped more
    /// than Agent:Evals:MaxDropPoints (5) below the baseline. Otherwise it is 0.
    ///
    /// The host program (the API's Program.cs, from Phase 3) calls IsEvalCommand and RunAsync.
    /// </summary>
    public static class EvalCommandLine
    {
        /// <summary>The argument that switches the program into eval mode.</summary>
        public const string EvalsSwitch = "--evals";

        /// <summary>True if the program was started with --evals.</summary>
        public static bool IsEvalCommand(string[] args)
        {
            return Array.IndexOf(args, EvalsSwitch) >= 0;
        }

        /// <summary>Runs the evals, prints the results and returns the exit code (0 = good, 1 = problem).</summary>
        public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, CancellationToken cancellationToken)
        {
            AgentOptions options = services.GetRequiredService<IOptions<AgentOptions>>().Value;
            ContentPathResolver paths = services.GetRequiredService<ContentPathResolver>();

            // ---- Read the command-line options ----
            CommandLineSettings settings;
            try
            {
                settings = CommandLineSettings.Parse(args, options.Evals);
            }
            catch (ArgumentException ex)
            {
                output.WriteLine("Error: " + ex.Message);
                return 1;
            }

            // ---- Load the cases ----
            string casesPath = paths.Resolve(settings.CasesPath);
            List<EvalCase> cases;
            try
            {
                cases = EvalCaseLoader.LoadFile(casesPath);
            }
            catch (Exception ex)
            {
                output.WriteLine("Error: could not load eval cases from " + casesPath + ": " + ex.Message);
                return 1;
            }

            // ---- Run them ----
            EvalRunner runner = services.GetRequiredService<EvalRunner>();
            EvalRunRequest request = new EvalRunRequest();
            request.Cases = cases;
            request.Repeat = settings.Repeat;
            request.UseJudge = settings.UseJudge;

            output.WriteLine("Running " + cases.Count + " eval cases" + (settings.UseJudge ? " with the LLM judge" : "") + "...");
            EvalRunRecord run = await runner.RunAsync(request, cancellationToken);
            List<EvalCaseResultRecord> results = await LoadResultsAsync(services, run.Id, cancellationToken);

            PrintResults(output, run, results);

            if (run.Status != EvalRunStatus.Completed)
            {
                output.WriteLine("FAILED: the eval run did not complete (" + run.Error + ").");
                return 1;
            }

            // ---- Save a new baseline, or compare with the saved one ----
            string baselinePath = paths.Resolve(settings.BaselinePath);
            if (settings.SaveBaseline)
            {
                EvalBaseline.FromRun(run, results).Save(baselinePath);
                output.WriteLine("Baseline saved to " + baselinePath);
                return 0;
            }

            if (settings.CompareBaseline)
                return CompareWithBaseline(output, run, results, baselinePath, settings.MaxDropPoints);

            return 0;
        }

        /// <summary>Compares the run with the saved baseline. Returns 1 for a regression or a missing baseline.</summary>
        private static int CompareWithBaseline(TextWriter output, EvalRunRecord run, List<EvalCaseResultRecord> results, string baselinePath, double maxDropPoints)
        {
            if (!File.Exists(baselinePath))
            {
                output.WriteLine("FAILED: no baseline at " + baselinePath + ". Run with --save-baseline first.");
                return 1;
            }

            EvalBaseline baseline = EvalBaseline.Load(baselinePath);
            output.WriteLine("Baseline pass rate: " + Percent(baseline.PassRate) + " (saved " + baseline.CreatedAt.ToString("yyyy-MM-dd HH:mm") + " UTC, model " + baseline.Model + ")");

            foreach (string worse in baseline.FindWorseCases(results))
                output.WriteLine("  worse than baseline: " + worse);

            if (EvalBaseline.IsRegression(baseline.PassRate, run.PassRate, maxDropPoints))
            {
                output.WriteLine("REGRESSION: the pass rate dropped more than " + maxDropPoints + " points below the baseline.");
                return 1;
            }

            output.WriteLine("OK: no regression against the baseline.");
            return 0;
        }

        /// <summary>Reads the case results from the store (in a short scope, because the store may be scoped).</summary>
        private static async Task<List<EvalCaseResultRecord>> LoadResultsAsync(IServiceProvider services, Guid runId, CancellationToken cancellationToken)
        {
            using (IServiceScope scope = services.CreateScope())
            {
                IEvalRunStore store = scope.ServiceProvider.GetRequiredService<IEvalRunStore>();
                return await store.GetCaseResultsAsync(runId, cancellationToken);
            }
        }

        /// <summary>Prints one line per case, then the summary.</summary>
        private static void PrintResults(TextWriter output, EvalRunRecord run, List<EvalCaseResultRecord> results)
        {
            output.WriteLine();
            foreach (EvalCaseResultRecord result in results)
            {
                string label = result.Passed ? "PASS " : (result.Flaky ? "FLAKY" : "FAIL ");
                output.WriteLine(label + " " + result.CaseId.PadRight(12) + " " + result.PassCount + "/" + result.RepeatCount + "  [" + result.Category + "]");

                foreach (EvalCheck check in result.Checks)
                {
                    if (!check.Passed)
                        output.WriteLine("        x " + check.Name + "  " + check.Detail);
                }
            }

            output.WriteLine();
            output.WriteLine("Model: " + run.Model + ", repeat: " + run.Repeat);
            output.WriteLine("Pass rate (attempts): " + Percent(run.PassRate) + " (" + run.PassedAttempts + "/" + run.TotalAttempts + ")");
            output.WriteLine("Cases passed every time: " + run.PassedCases + "/" + run.TotalCases + ", flaky: " + run.FlakyCases);
            if (run.CorrectToolRate.HasValue)
                output.WriteLine("Correct-tool rate: " + Percent(run.CorrectToolRate.Value));
            if (run.MadeUpRate.HasValue)
                output.WriteLine("Made-up prices/stock numbers: " + run.MadeUpFacts + " of " + run.FactsChecked + " (" + Percent(run.MadeUpRate.Value) + ")");
            output.WriteLine("Latency: median " + run.MedianLatencyMs.ToString("F0", CultureInfo.InvariantCulture) + " ms, p95 "
                + run.P95LatencyMs.ToString("F0", CultureInfo.InvariantCulture) + " ms. Median tokens: " + run.MedianTokens.ToString("F0", CultureInfo.InvariantCulture));
            output.WriteLine("Estimated cost: $" + run.TotalCostUsd.ToString("F4", CultureInfo.InvariantCulture));
        }

        /// <summary>0.8765 -> "87.7%".</summary>
        private static string Percent(double rate)
        {
            return (rate * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The options read from the command line.</summary>
        private sealed class CommandLineSettings
        {
            public int? Repeat { get; private set; }
            public bool UseJudge { get; private set; }
            public bool SaveBaseline { get; private set; }
            public bool CompareBaseline { get; private set; }
            public string BaselinePath { get; private set; }
            public string CasesPath { get; private set; }
            public double MaxDropPoints { get; private set; }

            /// <summary>Reads the arguments. Throws ArgumentException for a bad value.</summary>
            public static CommandLineSettings Parse(string[] args, AgentEvalOptions defaults)
            {
                CommandLineSettings settings = new CommandLineSettings();
                settings.BaselinePath = defaults.BaselinePath;
                settings.CasesPath = defaults.CasesPath;
                settings.MaxDropPoints = defaults.MaxDropPoints;

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--repeat":
                            int repeat;
                            if (!int.TryParse(NextValue(args, ref i), out repeat) || repeat < 1)
                                throw new ArgumentException("--repeat needs a whole number of 1 or more.");
                            settings.Repeat = repeat;
                            break;
                        case "--judge":
                            settings.UseJudge = true;
                            break;
                        case "--save-baseline":
                            settings.SaveBaseline = true;
                            break;
                        case "--baseline":
                            settings.CompareBaseline = true;
                            // The path is optional: "--baseline" alone uses the configured path.
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                                settings.BaselinePath = args[++i];
                            break;
                        case "--cases":
                            settings.CasesPath = NextValue(args, ref i);
                            break;
                    }
                }

                return settings;
            }

            /// <summary>Returns the value after a switch, e.g. the "5" in "--repeat 5".</summary>
            private static string NextValue(string[] args, ref int i)
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException(args[i] + " needs a value.");

                i++;
                return args[i];
            }
        }
    }
}
