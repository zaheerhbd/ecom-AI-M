using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Observability;
using Agent.Tools;

namespace Agent
{
    /// <summary>
    /// The names of the engines that can run the agent loop (setting Agent:Engine).
    /// </summary>
    public static class AgentEngines
    {
        /// <summary>Our own hand-written loop in AgentRunner (the default).</summary>
        public const string Custom = "Custom";

        /// <summary>Microsoft Agent Framework (project Agent.Framework).</summary>
        public const string AgentFramework = "AgentFramework";

        // The Model column in the database holds 50 characters, so the label must fit.
        private const int MaxModelLabelLength = 50;
        private const string AgentFrameworkSuffix = " (Agent Framework)";

        /// <summary>True for "Custom", and for an empty setting (the default).</summary>
        public static bool IsCustom(string engine)
        {
            return string.IsNullOrWhiteSpace(engine) || string.Equals(engine.Trim(), Custom, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True for "AgentFramework".</summary>
        public static bool IsAgentFramework(string engine)
        {
            return engine != null && string.Equals(engine.Trim(), AgentFramework, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The model name saved with runs and eval runs. With Agent Framework it gets a suffix,
        /// e.g. "gemini-3.5-flash (Agent Framework)", so the Runs and Evals pages show which engine
        /// answered without a new database column. Never longer than 50 characters.
        /// </summary>
        public static string ModelLabel(string model, string engine)
        {
            if (model == null)
                model = "";
            if (!IsAgentFramework(engine))
                return model;

            int room = MaxModelLabelLength - AgentFrameworkSuffix.Length;
            if (model.Length > room)
                model = model.Substring(0, room);
            return model + AgentFrameworkSuffix;
        }
    }

    /// <summary>
    /// A replacement for the "think -> act -> observe" loop inside AgentRunner (Phase 7).
    ///
    /// AgentRunner still does everything AROUND the loop for every engine: input guardrails, the
    /// session-owner check, memory, output guardrails, the groundedness check, shrinking old tool
    /// results, the run record and error handling. Only the loop in the middle is swapped, so the
    /// API answer looks exactly the same whichever engine is used.
    /// </summary>
    public interface IAgentLoop
    {
        /// <summary>The engine name this loop answers to, e.g. "AgentFramework".</summary>
        string EngineName { get; }

        /// <summary>
        /// Answers the latest user message in run.History. Must ADD every new message (tool requests,
        /// tool results, the final answer) to run.History, record steps and tokens on run.Result,
        /// and return the final answer text.
        /// </summary>
        Task<string> RunAsync(AgentLoopRun run, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Everything a replacement loop needs for ONE user message, plus helpers that record steps,
    /// tokens and metrics exactly the way the custom loop does.
    /// </summary>
    public sealed class AgentLoopRun
    {
        private readonly AgentRunner _runner;
        private readonly List<RanToolResult> _toolResults = new List<RanToolResult>();

        internal AgentLoopRun(AgentRunner runner, List<ChatMessage> history, AgentResult result, IReadOnlyList<ToolDefinition> tools,
            ToolRegistry registry, AgentRequestContext context, AgentOptions options, ILlmClient llm)
        {
            _runner = runner;
            History = history;
            Result = result;
            Tools = tools;
            Registry = registry;
            Context = context;
            Options = options;
            Llm = llm;
        }

        /// <summary>The conversation so far: system prompt, earlier turns, and the new (cleaned) user message last.</summary>
        public List<ChatMessage> History { get; }

        /// <summary>The answer being built: steps, tokens, tools used, stop reason.</summary>
        public AgentResult Result { get; }

        /// <summary>The tools THIS user may use (sign-in tools are left out for anonymous users).</summary>
        public IReadOnlyList<ToolDefinition> Tools { get; }

        /// <summary>Runs a tool with the sign-in rule and the timeout. Never throws for tool problems.</summary>
        public ToolRegistry Registry { get; }

        /// <summary>Who is asking (from the JWT) and where the sources go.</summary>
        public AgentRequestContext Context { get; }

        /// <summary>The agent settings: limits, temperature, app name.</summary>
        public AgentOptions Options { get; }

        /// <summary>The configured model (Gemini, OpenAI, Azure OpenAI or the Fake model).</summary>
        public ILlmClient Llm { get; }

        /// <summary>Tool results that really ran, so AgentRunner can shrink them later.</summary>
        internal List<RanToolResult> ToolResults
        {
            get { return _toolResults; }
        }

        /// <summary>The system prompt plus the most recent messages, cut the same way as the custom loop does.</summary>
        public List<ChatMessage> TrimHistory(List<ChatMessage> messages)
        {
            return _runner.TrimHistory(messages);
        }

        /// <summary>Adds a new, numbered step to the result.</summary>
        public AgentStep AddStep(string type, string name)
        {
            return AgentRunner.NewStep(Result, type, name);
        }

        /// <summary>Masks personal data and shortens long text before it is saved in a run record.</summary>
        public static string MaskForRecord(string text)
        {
            return AgentRunner.MaskForRecord(text);
        }

        /// <summary>"tool name + sorted arguments": two calls with the same key are the same call.</summary>
        public static string ToolCallKey(ToolCall call)
        {
            return AgentRunner.ToolCallKey(call);
        }

        /// <summary>Remembers that a tool really ran, so its result is shrunk to a stub after the answer.</summary>
        public void MarkToolResult(string toolCallId, string toolName)
        {
            RanToolResult ranTool = new RanToolResult();
            ranTool.ToolCallId = toolCallId;
            ranTool.ToolName = toolName;
            _toolResults.Add(ranTool);
        }

        /// <summary>
        /// Records one LLM call the same way the custom loop does: tokens on the result, GenAI metrics,
        /// and an "llm call N" step saying what the model decided.
        /// </summary>
        public void RecordLlmCall(int stepNumber, int messagesSent, int toolsAvailable, LlmResponse response, double durationMs)
        {
            Result.InputTokens += response.InputTokens;
            Result.OutputTokens += response.OutputTokens;

            KeyValuePair<string, object> operation = new KeyValuePair<string, object>(AgentTelemetry.OperationNameTag, AgentTelemetry.ChatOperation);
            KeyValuePair<string, object> model = new KeyValuePair<string, object>(AgentTelemetry.RequestModelTag, Llm.Model);
            AgentTelemetry.TokenUsage.Record(response.InputTokens, operation, model,
                new KeyValuePair<string, object>(AgentTelemetry.TokenTypeTag, "input"));
            AgentTelemetry.TokenUsage.Record(response.OutputTokens, operation, model,
                new KeyValuePair<string, object>(AgentTelemetry.TokenTypeTag, "output"));
            AgentTelemetry.OperationDuration.Record(durationMs / 1000.0, operation, model);

            // What the model decided: either "wants tool(s): ..." or its answer text.
            IReadOnlyList<ToolCall> toolCalls = response.Message.ToolCalls;
            string decision;
            if (toolCalls != null && toolCalls.Count > 0)
                decision = "wants tool(s): " + AgentRunner.DescribeToolCalls(toolCalls);
            else if (response.Message.Content != null)
                decision = response.Message.Content;
            else
                decision = "";

            AgentStep step = AddStep(AgentStepTypes.Llm, "llm call " + stepNumber);
            step.Input = messagesSent + " messages, " + toolsAvailable + " tools available";
            step.Output = MaskForRecord(decision);
            step.InputTokens = response.InputTokens;
            step.OutputTokens = response.OutputTokens;
            step.DurationMs = Math.Round(durationMs, 1);
        }
    }

    /// <summary>A tool that really ran in this answer: which request it answered, and which tool it was.</summary>
    internal sealed class RanToolResult
    {
        /// <summary>The id of the model's tool request (the tool result message quotes it).</summary>
        public string ToolCallId { get; set; }

        /// <summary>The tool's name, e.g. "search_products".</summary>
        public string ToolName { get; set; }
    }
}
