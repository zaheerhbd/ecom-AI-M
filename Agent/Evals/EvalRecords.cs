using System;
using System.Collections.Generic;

namespace Agent.Evals
{
    /// <summary>The states an eval run can be in.</summary>
    public static class EvalRunStatus
    {
        /// <summary>Still going. The UI polls until this changes.</summary>
        public const string Running = "Running";

        /// <summary>Every case ran.</summary>
        public const string Completed = "Completed";

        /// <summary>Something crashed part way through.</summary>
        public const string Failed = "Failed";
    }

    /// <summary>
    /// One eval run: its progress while it runs, and its summary when it has finished.
    /// A plain class so the Infrastructure project can map it to the EvalRuns table.
    /// </summary>
    public sealed class EvalRunRecord
    {
        /// <summary>The run id.</summary>
        public Guid Id { get; set; }

        /// <summary>When it started, in UTC.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>When it finished, in UTC. Null while running.</summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>Running, Completed or Failed (see EvalRunStatus).</summary>
        public string Status { get; set; }

        /// <summary>The exception type if the run failed.</summary>
        public string Error { get; set; }

        /// <summary>True if the LLM judge scored the answers.</summary>
        public bool UseJudge { get; set; }

        /// <summary>The default number of times each case runs.</summary>
        public int Repeat { get; set; }

        /// <summary>The model being tested.</summary>
        public string Model { get; set; }

        /// <summary>How many cases there are.</summary>
        public int TotalCases { get; set; }

        /// <summary>How many have finished (for the progress bar).</summary>
        public int CompletedCases { get; set; }

        /// <summary>Cases that passed EVERY time they ran.</summary>
        public int PassedCases { get; set; }

        /// <summary>Cases that passed SOME of the time: a sign the agent is unreliable there.</summary>
        public int FlakyCases { get; set; }

        /// <summary>Every single attempt (cases x repeats).</summary>
        public int TotalAttempts { get; set; }

        /// <summary>Attempts that passed.</summary>
        public int PassedAttempts { get; set; }

        /// <summary>
        /// PassedAttempts / TotalAttempts, from 0 to 1. This is the number the regression gate
        /// compares with the baseline: it moves smoothly, even when a single case is flaky.
        /// </summary>
        public double PassRate { get; set; }

        /// <summary>Of the attempts that checked tools, the share that called the right tools. Null if no case checked tools.</summary>
        public double? CorrectToolRate { get; set; }

        /// <summary>The middle latency of all attempts, in milliseconds.</summary>
        public double MedianLatencyMs { get; set; }

        /// <summary>95% of attempts were at least this fast, in milliseconds.</summary>
        public double P95LatencyMs { get; set; }

        /// <summary>The middle token count of all attempts.</summary>
        public double MedianTokens { get; set; }

        /// <summary>Estimated cost of the whole run (agent + judge), in US dollars.</summary>
        public decimal TotalCostUsd { get; set; }
    }

    /// <summary>
    /// The result for one case, summarised over all its repeats.
    /// A plain class so the Infrastructure project can map it to the EvalCaseResults table.
    /// </summary>
    public sealed class EvalCaseResultRecord
    {
        /// <summary>Starts with an empty check list.</summary>
        public EvalCaseResultRecord()
        {
            Checks = new List<EvalCheck>();
        }

        /// <summary>Database id (set by the database store).</summary>
        public int Id { get; set; }

        /// <summary>The eval run this belongs to.</summary>
        public Guid EvalRunId { get; set; }

        /// <summary>The case id, e.g. "TC-004".</summary>
        public string CaseId { get; set; }

        /// <summary>The case's group.</summary>
        public string Category { get; set; }

        /// <summary>The message sent to the agent.</summary>
        public string Input { get; set; }

        /// <summary>True only if EVERY repeat passed.</summary>
        public bool Passed { get; set; }

        /// <summary>True if some repeats passed and some failed.</summary>
        public bool Flaky { get; set; }

        /// <summary>How many times the case ran.</summary>
        public int RepeatCount { get; set; }

        /// <summary>How many of those passed, e.g. 4 (shown as "4/5").</summary>
        public int PassCount { get; set; }

        /// <summary>The checks of one attempt: the first failing one if any failed, otherwise the last one.</summary>
        public List<EvalCheck> Checks { get; set; }

        /// <summary>The answer from that same attempt.</summary>
        public string Answer { get; set; }

        /// <summary>The judge's score (1-5) for that attempt, or null when the judge did not run.</summary>
        public int? JudgeScore { get; set; }

        /// <summary>The agent run of that attempt, so the UI can open its full timeline.</summary>
        public Guid? AgentRunId { get; set; }

        /// <summary>The middle latency of the repeats, in milliseconds.</summary>
        public double DurationMs { get; set; }

        /// <summary>The middle token count of the repeats.</summary>
        public double Tokens { get; set; }
    }

    /// <summary>One page of eval runs, newest first.</summary>
    public sealed class EvalRunPage
    {
        /// <summary>Creates a page.</summary>
        public EvalRunPage(IReadOnlyList<EvalRunRecord> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }

        /// <summary>The runs on this page.</summary>
        public IReadOnlyList<EvalRunRecord> Items { get; }

        /// <summary>How many runs there are in total.</summary>
        public int TotalCount { get; }

        /// <summary>The page number, starting at 1.</summary>
        public int Page { get; }

        /// <summary>How many runs per page.</summary>
        public int PageSize { get; }
    }
}
