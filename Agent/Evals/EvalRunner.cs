using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Evals
{
    /// <summary>What to run: the cases, how many times each, and whether the judge scores them.</summary>
    public sealed class EvalRunRequest
    {
        /// <summary>Starts with no cases and the default settings.</summary>
        public EvalRunRequest()
        {
            Cases = new List<EvalCase>();
        }

        /// <summary>The cases to run.</summary>
        public List<EvalCase> Cases { get; set; }

        /// <summary>How many times to run each case. Null = AgentOptions.Evals.DefaultRepeat (3).</summary>
        public int? Repeat { get; set; }

        /// <summary>True = an extra LLM call scores each answer from 1 to 5. Costs extra, so off by default.</summary>
        public bool UseJudge { get; set; }
    }

    /// <summary>
    /// EVAL RUNNER. EVALS are automated tests for AI behaviour. When you change a prompt, a model
    /// or a tool, something that used to work can silently break. Evals catch that, the same way
    /// unit tests catch broken code.
    ///
    /// How it works:
    ///  - every case runs N times ("repeat"), because LLM answers change from run to run;
    ///  - every attempt gets its OWN DI scope, so it gets a fresh AgentRequestContext, ToolRegistry
    ///    and database context, exactly like a real chat request;
    ///  - progress is saved after every case, so the UI can poll it;
    ///  - only ONE eval run may go at a time, to protect the API key's rate limit and your money.
    ///
    /// This class is a singleton. It never holds on to scoped services (like a database store):
    /// it opens a short scope each time it needs one.
    /// </summary>
    public sealed class EvalRunner
    {
        private const int MaxRepeat = 10;

        private readonly IServiceScopeFactory _scopes;
        private readonly AgentOptions _options;
        private readonly ILogger<EvalRunner> _logger;
        private int _isRunning; // 0 = idle, 1 = running. Changed with Interlocked so two requests can't both start.

        /// <summary>Gets what it needs from DI.</summary>
        public EvalRunner(IServiceScopeFactory scopes, IOptions<AgentOptions> options, ILogger<EvalRunner> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>True while an eval run is going.</summary>
        public bool IsRunning
        {
            get { return Volatile.Read(ref _isRunning) == 1; }
        }

        /// <summary>
        /// Starts a run in the background and returns at once with its record (status Running).
        /// Poll the eval run store with the returned id to follow its progress.
        /// Throws InvalidOperationException if another run is already going.
        /// </summary>
        public async Task<EvalRunRecord> StartInBackgroundAsync(EvalRunRequest request)
        {
            AcquireRunSlot();
            try
            {
                EvalRunRecord run = CreateRunRecord(request);
                await SaveRunAsync(run);

                // Fire and forget: the HTTP request returns now, the run carries on by itself.
                // CancellationToken.None, because the HTTP request that started it will end straight away.
                Task background = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteAsync(run, request, CancellationToken.None);
                    }
                    finally
                    {
                        ReleaseRunSlot();
                    }
                });

                return run;
            }
            catch
            {
                ReleaseRunSlot(); // the background task never started, so free the slot here
                throw;
            }
        }

        /// <summary>
        /// Runs everything and waits for the end. Used by the command-line regression gate and the tests.
        /// Throws InvalidOperationException if another run is already going.
        /// </summary>
        public async Task<EvalRunRecord> RunAsync(EvalRunRequest request, CancellationToken cancellationToken)
        {
            AcquireRunSlot();
            try
            {
                EvalRunRecord run = CreateRunRecord(request);
                await SaveRunAsync(run);
                await ExecuteAsync(run, request, cancellationToken);
                return run;
            }
            finally
            {
                ReleaseRunSlot();
            }
        }

        /// <summary>Runs every case, saving progress as it goes, then works out the summary numbers.</summary>
        private async Task ExecuteAsync(EvalRunRecord run, EvalRunRequest request, CancellationToken cancellationToken)
        {
            List<EvalAttempt> allAttempts = new List<EvalAttempt>();

            try
            {
                foreach (EvalCase evalCase in request.Cases)
                {
                    // ---- Run the case N times ----
                    int repeat = ClampRepeat(evalCase.Repeat ?? run.Repeat);
                    List<EvalAttempt> attempts = new List<EvalAttempt>();
                    for (int i = 0; i < repeat; i++)
                        attempts.Add(await RunAttemptAsync(run, evalCase, cancellationToken));

                    allAttempts.AddRange(attempts);

                    // ---- Save the case result and the progress ----
                    EvalCaseResultRecord caseResult = SummariseCase(run.Id, evalCase, attempts);
                    await SaveCaseResultAsync(caseResult);
                    UpdateProgress(run, caseResult, attempts);
                    await SaveRunAsync(run);
                }

                run.Status = EvalRunStatus.Completed;
            }
            catch (Exception ex)
            {
                // Mark the run as failed (instead of leaving it "Running" forever) and log the details.
                _logger.LogError(ex, "Eval run {EvalRunId} failed", run.Id);
                run.Status = EvalRunStatus.Failed;
                run.Error = ex.GetType().Name;
            }

            // ---- Summary numbers over every attempt ----
            FillSummary(run, allAttempts);
            run.FinishedAt = DateTime.UtcNow;
            await SaveRunAsync(run);
        }

        /// <summary>
        /// Runs one case once and grades it. Every message gets its own DI scope, exactly like one
        /// HTTP request each. A multi-turn case sends its turns in ONE session, so the agent's memory
        /// is tested; the checks apply to the last answer.
        /// </summary>
        private async Task<EvalAttempt> RunAttemptAsync(EvalRunRecord run, EvalCase evalCase, CancellationToken cancellationToken)
        {
            // A fresh session for every attempt, so attempts don't influence each other.
            string sessionId = "eval-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            List<string> messages = evalCase.Messages();
            AgentResult result = null;
            decimal cost = 0;
            List<int> inputTokensPerTurn = new List<int>();

            foreach (string message in messages)
            {
                result = await RunOneMessageAsync(run, evalCase, sessionId, message, cancellationToken);
                cost += result.EstimatedCostUsd;
                inputTokensPerTurn.Add(result.InputTokens);
            }

            if (evalCase.IsMultiTurn)
                _logger.LogInformation("Eval case {CaseId}: input tokens per turn {Tokens}", evalCase.Id, string.Join(", ", inputTokensPerTurn));

            using (IServiceScope scope = _scopes.CreateScope())
            {
                // Code-based checks first, then the optional judge.
                List<EvalCheck> checks = Graders.Grade(evalCase, result);
                int? judgeScore = null;

                ILlmClient llm = scope.ServiceProvider.GetRequiredService<ILlmClient>();
                bool judgeCanRun = run.UseJudge && !evalCase.ShouldBlock && !(llm is FakeLlmClient);
                if (judgeCanRun)
                {
                    JudgeResult judge = await Graders.JudgeAsync(llm, _options.AppName, evalCase.Input, result.Answer, cancellationToken);
                    judgeScore = judge.Score;
                    cost += _options.Pricing.EstimateCost(judge.InputTokens, judge.OutputTokens);

                    bool judgePassed = judge.Score.HasValue && judge.Score.Value >= _options.Evals.JudgePassScore;
                    string scoreText = judge.Score.HasValue ? judge.Score.Value.ToString() : "no score";
                    checks.Add(new EvalCheck("judge score >= " + _options.Evals.JudgePassScore, judgePassed, "score " + scoreText));
                }

                return new EvalAttempt(result, checks, judgeScore, cost, evalCase.ExpectedTools != null);
            }
        }

        /// <summary>Sends one message as the case's user, in its own scope, marked as an eval.</summary>
        private async Task<AgentResult> RunOneMessageAsync(EvalRunRecord run, EvalCase evalCase, string sessionId, string message,
            CancellationToken cancellationToken)
        {
            using (IServiceScope scope = _scopes.CreateScope())
            {
                // Set up the request exactly like a chat request, but marked as an eval.
                AgentRequestContext context = scope.ServiceProvider.GetRequiredService<AgentRequestContext>();
                context.UserEmail = evalCase.SignedInAs;
                context.Channel = AgentRequestContext.EvalChannel;
                context.EvalRunId = run.Id;

                AgentRunner agent = scope.ServiceProvider.GetRequiredService<AgentRunner>();
                return await agent.RunAsync(sessionId, message, cancellationToken);
            }
        }

        /// <summary>Turns the attempts of one case into one result row: passes out of N, flaky or not.</summary>
        private static EvalCaseResultRecord SummariseCase(Guid evalRunId, EvalCase evalCase, List<EvalAttempt> attempts)
        {
            int passCount = 0;
            EvalAttempt shown = null; // the attempt whose checks and answer we show
            List<double> durations = new List<double>();
            List<double> tokens = new List<double>();

            foreach (EvalAttempt attempt in attempts)
            {
                if (attempt.Passed)
                    passCount++;
                else if (shown == null)
                    shown = attempt; // the FIRST failure is the most useful one to look at

                durations.Add(attempt.Result.DurationMs);
                tokens.Add(attempt.Result.InputTokens + attempt.Result.OutputTokens);
            }

            if (shown == null)
                shown = attempts[attempts.Count - 1];

            EvalCaseResultRecord record = new EvalCaseResultRecord();
            record.EvalRunId = evalRunId;
            record.CaseId = evalCase.Id;
            record.Category = evalCase.Category;
            record.Input = evalCase.Input;
            record.RepeatCount = attempts.Count;
            record.PassCount = passCount;
            record.Passed = passCount == attempts.Count;
            record.Flaky = passCount > 0 && passCount < attempts.Count;
            record.Checks = shown.Checks;
            record.Answer = shown.Result.Answer;
            record.JudgeScore = shown.JudgeScore;
            record.AgentRunId = shown.Result.RunId;
            record.DurationMs = Math.Round(EvalStatistics.Median(durations), 1);
            record.Tokens = EvalStatistics.Median(tokens);
            return record;
        }

        /// <summary>Adds one finished case to the running totals shown while the run is going.</summary>
        private static void UpdateProgress(EvalRunRecord run, EvalCaseResultRecord caseResult, List<EvalAttempt> attempts)
        {
            run.CompletedCases++;
            if (caseResult.Passed)
                run.PassedCases++;
            if (caseResult.Flaky)
                run.FlakyCases++;

            foreach (EvalAttempt attempt in attempts)
            {
                run.TotalAttempts++;
                if (attempt.Passed)
                    run.PassedAttempts++;
                run.TotalCostUsd += attempt.CostUsd;
            }

            run.PassRate = run.TotalAttempts == 0 ? 0 : Math.Round((double)run.PassedAttempts / run.TotalAttempts, 4);
        }

        /// <summary>Works out latency, token and correct-tool numbers over every attempt.</summary>
        private static void FillSummary(EvalRunRecord run, List<EvalAttempt> attempts)
        {
            List<double> durations = new List<double>();
            List<double> tokens = new List<double>();
            int toolAttempts = 0;
            int correctToolAttempts = 0;
            int factsChecked = 0;
            int madeUp = 0;

            foreach (EvalAttempt attempt in attempts)
            {
                durations.Add(attempt.Result.DurationMs);
                tokens.Add(attempt.Result.InputTokens + attempt.Result.OutputTokens);

                if (!attempt.Result.Blocked)
                {
                    factsChecked += attempt.Result.GroundedFactCount + attempt.Result.MadeUpFacts.Count;
                    madeUp += attempt.Result.MadeUpFacts.Count;
                }

                if (attempt.ChecksTools)
                {
                    toolAttempts++;
                    if (Graders.ToolChecksPassed(attempt.Checks))
                        correctToolAttempts++;
                }
            }

            run.MedianLatencyMs = Math.Round(EvalStatistics.Median(durations), 1);
            run.P95LatencyMs = Math.Round(EvalStatistics.Percentile(durations, 95), 1);
            run.MedianTokens = EvalStatistics.Median(tokens);
            run.CorrectToolRate = toolAttempts == 0 ? (double?)null : Math.Round((double)correctToolAttempts / toolAttempts, 4);
            run.FactsChecked = factsChecked;
            run.MadeUpFacts = madeUp;
            run.MadeUpRate = factsChecked == 0 ? (double?)null : Math.Round((double)madeUp / factsChecked, 4);
        }

        /// <summary>Creates the record for a new run (status Running).</summary>
        private EvalRunRecord CreateRunRecord(EvalRunRequest request)
        {
            EvalRunRecord run = new EvalRunRecord();
            run.Id = Guid.NewGuid();
            run.StartedAt = DateTime.UtcNow;
            run.Status = EvalRunStatus.Running;
            run.UseJudge = request.UseJudge;
            run.Repeat = ClampRepeat(request.Repeat ?? _options.Evals.DefaultRepeat);
            run.TotalCases = request.Cases.Count;
            run.Model = GetModelName();
            return run;
        }

        /// <summary>Keeps repeat between 1 and 10, so a typo can't start a huge, expensive run.</summary>
        private static int ClampRepeat(int repeat)
        {
            return Math.Min(Math.Max(repeat, 1), MaxRepeat);
        }

        /// <summary>Reads the model name from the LLM client (in a short scope).</summary>
        private string GetModelName()
        {
            using (IServiceScope scope = _scopes.CreateScope())
            {
                return scope.ServiceProvider.GetRequiredService<ILlmClient>().Model;
            }
        }

        /// <summary>Saves the run in a short scope (the database store is scoped).</summary>
        private async Task SaveRunAsync(EvalRunRecord run)
        {
            using (IServiceScope scope = _scopes.CreateScope())
            {
                IEvalRunStore store = scope.ServiceProvider.GetRequiredService<IEvalRunStore>();
                await store.SaveRunAsync(run, CancellationToken.None);
            }
        }

        /// <summary>Saves one case result in a short scope.</summary>
        private async Task SaveCaseResultAsync(EvalCaseResultRecord result)
        {
            using (IServiceScope scope = _scopes.CreateScope())
            {
                IEvalRunStore store = scope.ServiceProvider.GetRequiredService<IEvalRunStore>();
                await store.SaveCaseResultAsync(result, CancellationToken.None);
            }
        }

        /// <summary>Takes the single "running" slot, or throws if another run has it.</summary>
        private void AcquireRunSlot()
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                throw new InvalidOperationException("An eval run is already in progress. Wait for it to finish.");
        }

        /// <summary>Frees the "running" slot.</summary>
        private void ReleaseRunSlot()
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }

        /// <summary>One run of one case: the agent's result, the checks, and what it cost.</summary>
        private sealed class EvalAttempt
        {
            public EvalAttempt(AgentResult result, List<EvalCheck> checks, int? judgeScore, decimal costUsd, bool checksTools)
            {
                Result = result;
                Checks = checks;
                JudgeScore = judgeScore;
                CostUsd = costUsd;
                ChecksTools = checksTools;
            }

            public AgentResult Result { get; }
            public List<EvalCheck> Checks { get; }
            public int? JudgeScore { get; }
            public decimal CostUsd { get; }
            public bool ChecksTools { get; }

            public bool Passed
            {
                get
                {
                    foreach (EvalCheck check in Checks)
                    {
                        if (!check.Passed)
                            return false;
                    }

                    return true;
                }
            }
        }
    }
}
