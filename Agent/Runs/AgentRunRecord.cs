using System;
using System.Collections.Generic;
using Agent.Guardrails;

namespace Agent.Runs
{
    /// <summary>
    /// A saved copy of one agent run: the question, the answer and every step in between.
    /// The runs page lists these, and the run detail page draws the timeline from Steps.
    ///
    /// It is a plain class with no attributes, so the Infrastructure project can map it to a
    /// database table (AgentRuns) without the engine knowing anything about the database.
    /// </summary>
    public sealed class AgentRunRecord
    {
        /// <summary>Empty constructor, needed by EF Core and JSON.</summary>
        public AgentRunRecord()
        {
            ToolsUsed = new List<string>();
            Steps = new List<AgentStep>();
            GuardrailChecks = new List<GuardrailCheck>();
            Sources = new List<AgentSource>();
        }

        /// <summary>The run id (the same as AgentResult.RunId).</summary>
        public Guid Id { get; set; }

        /// <summary>When the run finished, in UTC.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The conversation id.</summary>
        public string SessionId { get; set; }

        /// <summary>The signed-in user's email, or null when anonymous.</summary>
        public string UserEmail { get; set; }

        /// <summary>"chat" or "eval".</summary>
        public string Channel { get; set; }

        /// <summary>The user's message, with personal data masked.</summary>
        public string Question { get; set; }

        /// <summary>The answer the user saw.</summary>
        public string Answer { get; set; }

        /// <summary>True if a guardrail blocked the input or the output.</summary>
        public bool Blocked { get; set; }

        /// <summary>The guardrail that blocked it, or null.</summary>
        public string BlockedBy { get; set; }

        /// <summary>Set when a safety limit stopped the run early ("max-steps", "token-budget"), or null.</summary>
        public string StopReason { get; set; }

        /// <summary>The exception type name if the run crashed. Never a stack trace.</summary>
        public string Error { get; set; }

        /// <summary>The model that answered.</summary>
        public string Model { get; set; }

        /// <summary>Tokens sent to the model, across all LLM calls.</summary>
        public int InputTokens { get; set; }

        /// <summary>Tokens the model wrote, across all LLM calls.</summary>
        public int OutputTokens { get; set; }

        /// <summary>Estimated cost in US dollars.</summary>
        public decimal EstimatedCostUsd { get; set; }

        /// <summary>Total time, in milliseconds.</summary>
        public double DurationMs { get; set; }

        /// <summary>The tools called, in order.</summary>
        public List<string> ToolsUsed { get; set; }

        /// <summary>Every step, in order.</summary>
        public List<AgentStep> Steps { get; set; }

        /// <summary>Every guardrail check, input and output.</summary>
        public List<GuardrailCheck> GuardrailChecks { get; set; }

        /// <summary>What the answer was based on.</summary>
        public List<AgentSource> Sources { get; set; }

        /// <summary>Set when the run was part of an eval.</summary>
        public Guid? EvalRunId { get; set; }

        /// <summary>Builds a record from a finished run and the request it belonged to.</summary>
        public static AgentRunRecord FromResult(AgentResult result, AgentRequestContext context)
        {
            AgentRunRecord record = new AgentRunRecord();
            record.Id = result.RunId;
            record.CreatedAt = DateTime.UtcNow;
            record.SessionId = result.SessionId;
            record.UserEmail = context.UserEmail;
            record.Channel = context.Channel;
            record.Question = result.Question;
            record.Answer = result.Answer;
            record.Blocked = result.Blocked;
            record.BlockedBy = result.BlockedBy;
            record.StopReason = result.StopReason;
            record.Error = result.Error;
            record.Model = result.Model;
            record.InputTokens = result.InputTokens;
            record.OutputTokens = result.OutputTokens;
            record.EstimatedCostUsd = result.EstimatedCostUsd;
            record.DurationMs = result.DurationMs;
            record.ToolsUsed = new List<string>(result.ToolsUsed);
            record.Steps = new List<AgentStep>(result.Steps);
            record.GuardrailChecks = new List<GuardrailCheck>(result.GuardrailChecks);
            record.Sources = new List<AgentSource>(result.Sources);
            record.EvalRunId = context.EvalRunId;
            return record;
        }
    }

    /// <summary>One page of runs, newest first.</summary>
    public sealed class AgentRunPage
    {
        /// <summary>Creates a page.</summary>
        public AgentRunPage(IReadOnlyList<AgentRunRecord> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }

        /// <summary>The runs on this page.</summary>
        public IReadOnlyList<AgentRunRecord> Items { get; }

        /// <summary>How many runs there are in total.</summary>
        public int TotalCount { get; }

        /// <summary>The page number, starting at 1.</summary>
        public int Page { get; }

        /// <summary>How many runs per page.</summary>
        public int PageSize { get; }
    }

    /// <summary>How often one tool was used.</summary>
    public sealed class ToolUsageCount
    {
        /// <summary>Creates a count.</summary>
        public ToolUsageCount(string name, int count)
        {
            Name = name;
            Count = count;
        }

        /// <summary>The tool's name.</summary>
        public string Name { get; }

        /// <summary>How many times it was called.</summary>
        public int Count { get; }
    }

    /// <summary>Totals across all saved runs, for the stats cards on the runs page.</summary>
    public sealed class AgentRunStats
    {
        /// <summary>Starts with zero everywhere.</summary>
        public AgentRunStats()
        {
            TopTools = new List<ToolUsageCount>();
        }

        /// <summary>How many runs are saved.</summary>
        public int TotalRuns { get; set; }

        /// <summary>How many were blocked by a guardrail.</summary>
        public int BlockedRuns { get; set; }

        /// <summary>How many crashed.</summary>
        public int ErrorRuns { get; set; }

        /// <summary>Blocked runs as a share of all runs, from 0 to 1.</summary>
        public double BlockRate { get; set; }

        /// <summary>Average time per run, in milliseconds.</summary>
        public double AverageDurationMs { get; set; }

        /// <summary>All input tokens added up.</summary>
        public long TotalInputTokens { get; set; }

        /// <summary>All output tokens added up.</summary>
        public long TotalOutputTokens { get; set; }

        /// <summary>All estimated costs added up, in US dollars.</summary>
        public decimal TotalEstimatedCostUsd { get; set; }

        /// <summary>The most used tools, most used first.</summary>
        public List<ToolUsageCount> TopTools { get; set; }
    }
}
