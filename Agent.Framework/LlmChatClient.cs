using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Observability;
using Microsoft.Extensions.AI;
using AgentChatMessage = Agent.Llm.ChatMessage;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Agent.Framework
{
    /// <summary>
    /// The MODEL ADAPTER: lets Microsoft Agent Framework talk to the model through OUR ILlmClient.
    ///
    /// Agent Framework works with any IChatClient (Microsoft's "talk to a model" interface). Instead
    /// of a second HTTP client, we wrap the one we already have. That gives the framework engine:
    ///  - every provider we support (Gemini, OpenAI, Azure OpenAI, and the free Fake model for tests);
    ///  - Gemini's thought signature, which our client already sends back on tool calls;
    ///  - the same friendly errors (LlmException: quota used up, bad key, provider down).
    ///
    /// It also does the custom loop's per-call jobs, so both engines record the same things:
    ///  - trims the history the same way before each call;
    ///  - records an "llm call N" step, tokens and GenAI metrics, inside a trace span;
    ///  - enforces the token budget and the tool-call limit (the step limit is the framework's own
    ///    MaximumIterationsPerRequest, see FrameworkAgentLoop). When one is hit, it answers with a polite
    ///    message ITSELF, without calling the model. The framework then sees a normal final answer and
    ///    stops, and the history stays valid (every tool request already has a result).
    ///
    /// One instance is made per user message.
    /// </summary>
    internal sealed class LlmChatClient : IChatClient
    {
        internal const string TokenBudgetAnswer = "That request needed more work than I'm allowed to do in one go. Could you simplify it?";
        internal const string ToolCallLimitAnswer = "Sorry, that needed more lookups than I'm allowed to do in one answer. Could you ask about fewer things at once?";

        private readonly AgentLoopRun _run;
        private readonly FrameworkTurn _turn;

        public LlmChatClient(AgentLoopRun run, FrameworkTurn turn)
        {
            _run = run;
            _turn = turn;
        }

        /// <summary>One request to the model. Agent Framework calls this once per step of its loop.</summary>
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // ---- Safety limits, checked before paying for another call ----
            if (_turn.HitToolCallLimit)
            {
                _run.Result.StopReason = AgentStopReasons.MaxToolCalls;
                return FinalAnswerWithoutModel(ToolCallLimitAnswer);
            }

            if (_run.Result.InputTokens + _run.Result.OutputTokens > _run.Options.MaxTokensPerMessage)
            {
                _run.Result.StopReason = AgentStopReasons.TokenBudget;
                return FinalAnswerWithoutModel(TokenBudgetAnswer);
            }

            _turn.LlmCalls++;

            // ---- Same history (trimmed the same way) and the tools this user may use ----
            List<AgentChatMessage> toSend = _run.TrimHistory(MessageMapper.ToAgent(messages));

            // The tools come from OUR list (same names, descriptions and schemas as the custom loop).
            // On the LAST allowed call the framework removes the tools so the model must answer in text:
            // that is the step limit being reached, so we record stop reason "max-steps".
            bool offersTools = false;
            if (options != null && options.Tools != null && options.Tools.Count > 0 && options.ToolMode != ChatToolMode.None)
                offersTools = true;

            IReadOnlyList<ToolDefinition> tools;
            if (offersTools)
            {
                tools = _run.Tools;
            }
            else
            {
                tools = new List<ToolDefinition>();
                if (_run.Tools.Count > 0)
                    _run.Result.StopReason = AgentStopReasons.MaxSteps;
            }

            // Temperature: the framework's value if it set one, otherwise ours.
            double? temperature = _run.Options.Temperature;
            if (options != null && options.Temperature.HasValue)
                temperature = options.Temperature.Value;

            // ---- Call the model (inside a trace span, like the custom loop) ----
            Stopwatch timer = Stopwatch.StartNew();
            LlmResponse response;
            using (Activity span = AgentTelemetry.ActivitySource.StartActivity(AgentTelemetry.ChatOperation + " " + _run.Llm.Model))
            {
                if (span != null)
                {
                    span.SetTag(AgentTelemetry.OperationNameTag, AgentTelemetry.ChatOperation);
                    span.SetTag(AgentTelemetry.RequestModelTag, _run.Llm.Model);
                }

                response = await _run.Llm.CompleteAsync(new LlmRequest(toSend, tools, temperature), cancellationToken);

                if (span != null)
                {
                    span.SetTag(AgentTelemetry.InputTokensTag, response.InputTokens);
                    span.SetTag(AgentTelemetry.OutputTokensTag, response.OutputTokens);
                }
            }

            _run.RecordLlmCall(_turn.LlmCalls, toSend.Count, tools.Count, response, timer.Elapsed.TotalMilliseconds);

            // ---- Hand the reply back in Microsoft's format ----
            ChatResponse chatResponse = new ChatResponse(MessageMapper.ToFramework(response.Message));
            chatResponse.ModelId = _run.Llm.Model;

            bool wantsTools = response.Message.ToolCalls != null && response.Message.ToolCalls.Count > 0;
            if (wantsTools)
                chatResponse.FinishReason = ChatFinishReason.ToolCalls;
            else
                chatResponse.FinishReason = ChatFinishReason.Stop;

            UsageDetails usage = new UsageDetails();
            usage.InputTokenCount = response.InputTokens;
            usage.OutputTokenCount = response.OutputTokens;
            usage.TotalTokenCount = response.InputTokens + response.OutputTokens;
            chatResponse.Usage = usage;

            return chatResponse;
        }

        /// <summary>A final text answer made here, without calling the model (and so without cost).</summary>
        private ChatResponse FinalAnswerWithoutModel(string text)
        {
            ChatResponse response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            response.ModelId = _run.Llm.Model;
            response.FinishReason = ChatFinishReason.Stop;
            return response;
        }

        /// <summary>
        /// Streaming (word by word) is not used: the shop API returns the whole answer at once,
        /// and ChatClientAgent.RunAsync uses GetResponseAsync.
        /// </summary>
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotSupportedException("The shop assistant does not stream answers.");
        }

        /// <summary>Lets the framework ask "are you an X?". We only answer for ourselves.</summary>
        public object GetService(Type serviceType, object serviceKey = null)
        {
            if (serviceKey == null && serviceType != null && serviceType.IsInstanceOfType(this))
                return this;

            return null;
        }

        /// <summary>Nothing to clean up: the real HTTP client is owned by DI.</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>What the framework engine keeps track of while answering ONE message.</summary>
    internal sealed class FrameworkTurn
    {
        public FrameworkTurn()
        {
            SeenCalls = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>Calls made to the model so far.</summary>
        public int LlmCalls { get; set; }

        /// <summary>Tool calls asked for so far in this answer (repeats included).</summary>
        public int ToolCallCount { get; set; }

        /// <summary>True once a call was refused because of MaxToolCallsPerRun.</summary>
        public bool HitToolCallLimit { get; set; }

        /// <summary>ToolCallKey of every call that really ran.</summary>
        public HashSet<string> SeenCalls { get; }
    }
}
