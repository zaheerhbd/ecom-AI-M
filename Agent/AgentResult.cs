using System;
using System.Collections.Generic;
using Agent.Guardrails;

namespace Agent
{
    /// <summary>The kinds of step the agent records. Plain strings so they read well in JSON and in the UI.</summary>
    public static class AgentStepTypes
    {
        /// <summary>A guardrail pipeline ran (input or output).</summary>
        public const string Guardrail = "guardrail";

        /// <summary>One call to the language model.</summary>
        public const string Llm = "llm";

        /// <summary>One tool ran.</summary>
        public const string Tool = "tool";
    }

    /// <summary>Why a run stopped before the model gave a normal final answer.</summary>
    public static class AgentStopReasons
    {
        /// <summary>The model kept asking for tools and hit MaxSteps.</summary>
        public const string MaxSteps = "max-steps";

        /// <summary>The run used more tokens than MaxTokensPerMessage.</summary>
        public const string TokenBudget = "token-budget";
    }

    /// <summary>
    /// One thing that happened while answering: a guardrail check, an LLM call or a tool call.
    /// Returning these makes the agent EXPLAINABLE: you can see every step it took, what went
    /// in, what came out, how long it took and how many tokens it used.
    /// </summary>
    public sealed class AgentStep
    {
        /// <summary>Empty constructor, needed when a saved run is read back from JSON.</summary>
        public AgentStep()
        {
        }

        /// <summary>Position in the run, starting at 1.</summary>
        public int Index { get; set; }

        /// <summary>"guardrail", "llm" or "tool" (see AgentStepTypes).</summary>
        public string Type { get; set; }

        /// <summary>A short name, e.g. "input guardrails", "llm call 2" or "search_products".</summary>
        public string Name { get; set; }

        /// <summary>What went in (for a tool: its JSON arguments). Personal data is masked.</summary>
        public string Input { get; set; }

        /// <summary>What came out (for a tool: its result). Personal data is masked.</summary>
        public string Output { get; set; }

        /// <summary>Tokens sent to the model (LLM steps only).</summary>
        public int InputTokens { get; set; }

        /// <summary>Tokens the model wrote (LLM steps only).</summary>
        public int OutputTokens { get; set; }

        /// <summary>How long the step took, in milliseconds.</summary>
        public double DurationMs { get; set; }

        /// <summary>True when a tool returned an error.</summary>
        public bool IsError { get; set; }
    }

    /// <summary>Everything about one answer: the text, plus how we got there and what it cost.</summary>
    public sealed class AgentResult
    {
        /// <summary>Creates an empty result for one run.</summary>
        public AgentResult(Guid runId, string sessionId)
        {
            RunId = runId;
            SessionId = sessionId;
            Question = "";
            Answer = "";
            TraceId = "";
            Model = "";
            ToolsUsed = new List<string>();
            Steps = new List<AgentStep>();
            GuardrailChecks = new List<GuardrailCheck>();
            Sources = new List<AgentSource>();
        }

        /// <summary>The id of this run. The run record is saved under this id.</summary>
        public Guid RunId { get; }

        /// <summary>Which conversation this answer belongs to.</summary>
        public string SessionId { get; }

        /// <summary>The user's message, with personal data masked (this is what gets saved).</summary>
        public string Question { get; set; }

        /// <summary>The text shown to the user.</summary>
        public string Answer { get; set; }

        /// <summary>True if a guardrail stopped the input or the output.</summary>
        public bool Blocked { get; set; }

        /// <summary>The name of the guardrail that blocked it (null when not blocked).</summary>
        public string BlockedBy { get; set; }

        /// <summary>Why the guardrail blocked it (null when not blocked).</summary>
        public string BlockedReason { get; set; }

        /// <summary>Set when a safety limit stopped the run early (see AgentStopReasons). Null for a normal answer.</summary>
        public string StopReason { get; set; }

        /// <summary>The names of the tools the model asked us to run, in order.</summary>
        public List<string> ToolsUsed { get; }

        /// <summary>Every step the agent took, so you can see how it reached the answer.</summary>
        public List<AgentStep> Steps { get; }

        /// <summary>The result of every guardrail check, input and output.</summary>
        public List<GuardrailCheck> GuardrailChecks { get; }

        /// <summary>The products, policies or orders the answer is based on.</summary>
        public List<AgentSource> Sources { get; }

        /// <summary>Tokens sent TO the model, across all LLM calls. Tokens are how LLM providers charge.</summary>
        public int InputTokens { get; set; }

        /// <summary>Tokens the model wrote back, across all LLM calls.</summary>
        public int OutputTokens { get; set; }

        /// <summary>Estimated cost in US dollars, from the prices in config.</summary>
        public decimal EstimatedCostUsd { get; set; }

        /// <summary>Total time to answer, in milliseconds.</summary>
        public double DurationMs { get; set; }

        /// <summary>The model that answered, e.g. "gpt-5.4-mini".</summary>
        public string Model { get; set; }

        /// <summary>Use this id to find the request in your logs or tracing tool.</summary>
        public string TraceId { get; set; }

        /// <summary>The exception type name if something crashed (null when all went well). Never a stack trace.</summary>
        public string Error { get; set; }
    }
}
