using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Agent.Observability
{
    /// <summary>
    /// OBSERVABILITY = being able to answer "what is my agent doing, how well, and how much does it cost?"
    ///
    /// Three pillars, all built into .NET (no packages needed):
    ///  1. LOGS    - ILogger messages ("tool search_products failed").
    ///  2. TRACES  - ActivitySource. One trace per request, with child "spans" for each LLM call
    ///               and each tool call, so you can see the whole story and where time went.
    ///  3. METRICS - Meter. Numbers over time: requests, tokens, tool calls, blocks, latency.
    ///
    /// The span and tag names follow the OpenTelemetry "GenAI" conventions (gen_ai.*). Tools such as
    /// Azure Application Insights understand these names, so in Phase 6 the API only has to add the
    /// exporter package and listen to the "Agent" source and meter. Nothing here needs to change.
    /// </summary>
    public static class AgentTelemetry
    {
        /// <summary>The name used for both the trace source and the meter.</summary>
        public const string Name = "Agent";

        // ---- Standard GenAI names (OpenTelemetry semantic conventions) ----

        /// <summary>What kind of operation a span is: "invoke_agent", "chat" or "execute_tool".</summary>
        public const string OperationNameTag = "gen_ai.operation.name";

        /// <summary>The model we asked for, e.g. "gpt-5.4-mini".</summary>
        public const string RequestModelTag = "gen_ai.request.model";

        /// <summary>Tokens sent to the model.</summary>
        public const string InputTokensTag = "gen_ai.usage.input_tokens";

        /// <summary>Tokens the model wrote.</summary>
        public const string OutputTokensTag = "gen_ai.usage.output_tokens";

        /// <summary>The tool's name, on execute_tool spans.</summary>
        public const string ToolNameTag = "gen_ai.tool.name";

        /// <summary>The model's id for this tool call, on execute_tool spans.</summary>
        public const string ToolCallIdTag = "gen_ai.tool.call.id";

        /// <summary>The assistant's name, on invoke_agent spans.</summary>
        public const string AgentNameTag = "gen_ai.agent.name";

        /// <summary>The conversation (session) id.</summary>
        public const string ConversationIdTag = "gen_ai.conversation.id";

        /// <summary>Says whether a token measurement is "input" or "output".</summary>
        public const string TokenTypeTag = "gen_ai.token.type";

        /// <summary>The operation name for a whole agent run.</summary>
        public const string InvokeAgentOperation = "invoke_agent";

        /// <summary>The operation name for one LLM call.</summary>
        public const string ChatOperation = "chat";

        /// <summary>The operation name for one tool call.</summary>
        public const string ExecuteToolOperation = "execute_tool";

        /// <summary>Creates trace spans ("Activities" in .NET).</summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(Name);

        /// <summary>Creates metric instruments (counters and histograms).</summary>
        public static readonly Meter Meter = new Meter(Name);

        // ---- Standard GenAI metrics ----

        /// <summary>Tokens per LLM call, tagged with gen_ai.token.type = input or output.</summary>
        public static readonly Histogram<long> TokenUsage = Meter.CreateHistogram<long>("gen_ai.client.token.usage", "{token}", "Tokens used per LLM call");

        /// <summary>How long each LLM call took, in seconds (the standard unit for this metric).</summary>
        public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>("gen_ai.client.operation.duration", "s", "Time per LLM call");

        // ---- Our own agent metrics ----

        /// <summary>Messages handled.</summary>
        public static readonly Counter<long> Requests = Meter.CreateCounter<long>("agent.requests", null, "Messages handled");

        /// <summary>Tools executed, tagged with the tool name and whether it worked.</summary>
        public static readonly Counter<long> ToolCalls = Meter.CreateCounter<long>("agent.tool_calls", null, "Tools executed");

        /// <summary>Messages blocked by guardrails, tagged with the stage and guardrail.</summary>
        public static readonly Counter<long> GuardrailBlocks = Meter.CreateCounter<long>("agent.guardrail_blocks", null, "Messages blocked by guardrails");

        /// <summary>Unexpected failures.</summary>
        public static readonly Counter<long> Errors = Meter.CreateCounter<long>("agent.errors", null, "Unexpected failures");

        /// <summary>End-to-end time per message, in milliseconds.</summary>
        public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("agent.request.duration", "ms", "End-to-end time per message");
    }
}
