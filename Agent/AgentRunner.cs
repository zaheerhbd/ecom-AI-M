using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Evals;
using Agent.Guardrails;
using Agent.Llm;
using Agent.Memory;
using Agent.Observability;
using Agent.Runs;
using Agent.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent
{
    /// <summary>
    /// THE AGENT. This file is the heart of the project, read it top to bottom.
    ///
    /// An "agent" is just a loop around an LLM:
    ///
    ///     user message
    ///         |
    ///     [input guardrails] --blocked--> safe reply
    ///         |
    ///     [session owner check] --someone else's chat--> safe reply
    ///         |
    ///     +--> ask the LLM (with history + list of tools this user may use)
    ///     |       |
    ///     |   LLM wants a tool? --yes--> run tool, add result to history --+
    ///     |       | no                                                     |
    ///     |   final answer                                                 |
    ///     +----------------------------------------------------------------+
    ///         |
    ///     [output guardrails] --blocked--> safe reply
    ///         |
    ///     save history, save run record, return answer
    ///
    /// Everything else (tools, guardrails, memory, run records, telemetry, evals) plugs into this loop.
    /// One runner is created per request ("scoped"), because the tools it uses are scoped.
    /// </summary>
    public sealed class AgentRunner
    {
        // Long tool results and answers are cut down in the saved steps, so run records stay small.
        private const int MaxStepTextLength = 4000;
        private const int MaxSessionIdLength = 64;
        private const string SessionOwnerCheck = "session-owner";

        private readonly ILlmClient _llm;
        private readonly ToolRegistry _tools;
        private readonly IConversationStore _memory;
        private readonly AgentGuardrails _guardrails;
        private readonly SystemPromptProvider _systemPrompt;
        private readonly IAgentRunStore _runStore;
        private readonly AgentRequestContext _context;
        private readonly AgentOptions _options;
        private readonly ILogger<AgentRunner> _logger;

        // Null = our own loop below (Agent:Engine "Custom"). Otherwise another engine's loop (Phase 7).
        private readonly IAgentLoop _externalLoop;

        /// <summary>All the parts the agent needs are passed in by Dependency Injection.</summary>
        public AgentRunner(ILlmClient llm, ToolRegistry tools, IConversationStore memory, AgentGuardrails guardrails,
            SystemPromptProvider systemPrompt, IAgentRunStore runStore, AgentRequestContext context,
            IOptions<AgentOptions> options, ILogger<AgentRunner> logger, IEnumerable<IAgentLoop> loops)
        {
            _llm = llm;
            _tools = tools;
            _memory = memory;
            _guardrails = guardrails;
            _systemPrompt = systemPrompt;
            _runStore = runStore;
            _context = context;
            _options = options.Value;
            _logger = logger;
            _externalLoop = FindLoop(_options.Engine, loops);
        }

        /// <summary>Null for the custom engine; otherwise the registered loop with that engine name.</summary>
        private static IAgentLoop FindLoop(string engine, IEnumerable<IAgentLoop> loops)
        {
            if (AgentEngines.IsCustom(engine))
                return null;

            if (loops != null)
            {
                foreach (IAgentLoop loop in loops)
                {
                    if (string.Equals(loop.EngineName, engine.Trim(), StringComparison.OrdinalIgnoreCase))
                        return loop;
                }
            }

            throw new InvalidOperationException("Agent:Engine is \"" + engine + "\", but no loop with that name is registered. "
                + "Use \"" + AgentEngines.Custom + "\", or register the engine (for Agent Framework: AddAgentFrameworkEngine()).");
        }

        /// <summary>
        /// Answers one user message in the given conversation.
        /// An empty or too-long session id is replaced with a new one; read it back from AgentResult.SessionId.
        /// </summary>
        public async Task<AgentResult> RunAsync(string sessionId, string userMessage, CancellationToken cancellationToken = default(CancellationToken))
        {
            Stopwatch timer = Stopwatch.StartNew();
            AgentResult result = new AgentResult(Guid.NewGuid(), NormaliseSessionId(sessionId));
            result.Model = AgentEngines.ModelLabel(_llm.Model, _options.Engine);

            // The question we SAVE always has personal data masked, even if a guardrail blocks it later.
            result.Question = MaskForRecord(userMessage);

            // OBSERVABILITY: start a trace for this whole request. Tool and LLM spans become its children.
            // A "trace" is the full story of one request; each piece of work inside it is a "span".
            using (Activity activity = AgentTelemetry.ActivitySource.StartActivity(AgentTelemetry.InvokeAgentOperation + " " + _options.AppName))
            {
                if (activity != null)
                {
                    activity.SetTag(AgentTelemetry.OperationNameTag, AgentTelemetry.InvokeAgentOperation);
                    activity.SetTag(AgentTelemetry.AgentNameTag, _options.AppName);
                    activity.SetTag(AgentTelemetry.ConversationIdTag, result.SessionId);
                    activity.SetTag(AgentTelemetry.RequestModelTag, result.Model);
                    result.TraceId = activity.TraceId.ToHexString();
                }

                AgentTelemetry.Requests.Add(1);

                try
                {
                    await AnswerAsync(result, userMessage ?? "", cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // The caller gave up (e.g. browser closed). Nothing to report or save.
                }
                catch (LlmException ex)
                {
                    // The LLM provider itself failed (quota used up, key/subscription problem, provider down).
                    // Show the user WHY, using the safe message the client built; keep the detail in the log only.
                    _logger.LogError(ex, "LLM call failed ({Kind}) for session {SessionId}", ex.Kind, result.SessionId);
                    AgentTelemetry.Errors.Add(1);
                    if (activity != null)
                        activity.SetStatus(ActivityStatusCode.Error, ex.Kind.ToString());
                    result.Answer = ex.UserMessage;
                    result.Error = "LlmException:" + ex.Kind;
                }
                catch (Exception ex)
                {
                    // Never show raw exception details to users. Log them, return something polite.
                    _logger.LogError(ex, "Agent failed for session {SessionId}", result.SessionId);
                    AgentTelemetry.Errors.Add(1);
                    if (activity != null)
                        activity.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                    result.Answer = "Sorry, something went wrong on our side. Please try again in a moment.";
                    result.Error = ex.GetType().Name;
                }

                // Totals that are known only at the end.
                result.DurationMs = Math.Round(timer.Elapsed.TotalMilliseconds, 1);
                result.EstimatedCostUsd = _options.Pricing.EstimateCost(result.InputTokens, result.OutputTokens);
                result.Sources.AddRange(_context.Sources);
                RecordFinishedRun(result, activity);
            }

            await SaveRunRecordAsync(result);
            return result;
        }

        /// <summary>The steps of answering: guardrails, owner check, memory, loop, guardrails, save.</summary>
        private async Task AnswerAsync(AgentResult result, string userMessage, CancellationToken cancellationToken)
        {
            // ---- STEP 1: INPUT GUARDRAILS ------------------------------------------------
            // GUARDRAILS are safety checks in plain code that run before and after the model.
            GuardrailPipelineResult input = RunGuardrails(_guardrails.Input, userMessage, result);
            if (!input.Allowed)
            {
                Block(result, input.BlockedBy, input.Reason, GuardrailStages.Input);
                return; // The LLM is never called, which also saves money.
            }

            // ---- STEP 2: LOAD MEMORY AND CHECK WHO OWNS IT --------------------------------
            // The model remembers nothing between calls, so we re-send the conversation each time.
            // A conversation belongs to the user who started it: nobody else may continue it.
            ConversationSession session = await _memory.GetAsync(result.SessionId, cancellationToken);
            if (session != null && !IsSameOwner(session.OwnerEmail, _context.UserEmail))
            {
                const string reason = "This conversation belongs to a different account.";
                result.GuardrailChecks.Add(new GuardrailCheck(GuardrailStages.Input, SessionOwnerCheck, GuardrailOutcome.Blocked, reason, 0));
                Block(result, SessionOwnerCheck, reason, GuardrailStages.Input);
                return;
            }

            List<ChatMessage> history = session != null ? session.Messages : new List<ChatMessage>();
            if (history.Count == 0)
                history.Add(ChatMessage.SystemMessage(_systemPrompt.GetPrompt()));

            history.Add(ChatMessage.UserMessage(input.Text)); // the CLEANED text (PII masked)

            // ---- STEP 3: THE AGENT LOOP (think -> act -> observe) ----------------------
            TurnState turn = new TurnState();
            string finalAnswer;
            if (_externalLoop == null)
                finalAnswer = await RunAgentLoopAsync(history, result, turn, cancellationToken);     // our own loop (Custom)
            else
                finalAnswer = await RunExternalLoopAsync(history, result, turn, cancellationToken);  // another engine (Phase 7)

            // ---- STEP 4: OUTPUT GUARDRAILS ----------------------------------------------
            GuardrailPipelineResult output = RunGuardrails(_guardrails.Output, finalAnswer, result);
            if (output.Allowed)
            {
                result.Answer = output.Text;
                CheckGroundedness(history, result);
            }
            else
            {
                Block(result, output.BlockedBy, output.Reason, GuardrailStages.Output);
            }

            // ---- STEP 5: SAVE MEMORY ----------------------------------------------------
            // Old tool results are replaced by short stubs first (they are re-sent with every later question).
            if (_options.ShrinkOldToolResults)
                ShrinkToolResults(history, turn);

            // Store what the USER actually saw, so a blocked answer never re-enters the conversation.
            ChatMessage lastMessage = history[history.Count - 1];
            if (lastMessage.Role == ChatRole.Assistant && lastMessage.ToolCalls == null)
                history[history.Count - 1] = ChatMessage.AssistantMessage(result.Answer);
            else
                history.Add(ChatMessage.AssistantMessage(result.Answer));

            await _memory.SaveAsync(result.SessionId, new ConversationSession(_context.UserEmail, TrimHistory(history)), cancellationToken);
        }

        /// <summary>
        /// Asks the model what to do, runs any tools it asks for, and repeats until the model
        /// gives a final answer or we hit a safety limit. Returns the final answer text.
        /// </summary>
        private async Task<string> RunAgentLoopAsync(List<ChatMessage> history, AgentResult result, TurnState turn, CancellationToken cancellationToken)
        {
            // Work out once which tools this user may see. Sign-in tools are hidden from anonymous users.
            IReadOnlyList<ToolDefinition> tools = _tools.GetDefinitions(_context);

            for (int step = 1; step <= _options.MaxSteps; step++)
            {
                // Cost guard: stop if this one message has already used too many tokens.
                if (result.InputTokens + result.OutputTokens > _options.MaxTokensPerMessage)
                {
                    _logger.LogWarning("Token budget exceeded for session {SessionId}", result.SessionId);
                    result.StopReason = AgentStopReasons.TokenBudget;
                    return "That request needed more work than I'm allowed to do in one go. Could you simplify it?";
                }

                // THINK: ask the model what to do next.
                ChatMessage reply = await AskModelAsync(step, history, tools, result, cancellationToken);

                // No tool requested = the model is done.
                bool wantsTools = reply.ToolCalls != null && reply.ToolCalls.Count > 0;
                if (!wantsTools)
                    return reply.Content ?? "";

                // ACT + OBSERVE: run each requested tool and give the results back to the model.
                // Tools run ONE AFTER ANOTHER, never in parallel: they share one database context.
                await RunRequestedToolsAsync(reply.ToolCalls, history, result, turn, cancellationToken);

                // Too many tool calls in one answer: stop safely (every call still got a result above).
                if (turn.HitToolCallLimit)
                {
                    _logger.LogWarning("Max tool calls ({MaxToolCalls}) reached for session {SessionId}", _options.MaxToolCallsPerRun, result.SessionId);
                    result.StopReason = AgentStopReasons.MaxToolCalls;
                    return "Sorry, that needed more lookups than I'm allowed to do in one answer. Could you ask about fewer things at once?";
                }
                // ...then loop: the model reads the tool results and decides what's next.
            }

            // The model kept asking for tools and never finished. Stop safely.
            _logger.LogWarning("Max steps ({MaxSteps}) reached for session {SessionId}", _options.MaxSteps, result.SessionId);
            result.StopReason = AgentStopReasons.MaxSteps;
            return "Sorry, I couldn't finish that. Could you rephrase or break it into smaller questions?";
        }

        /// <summary>
        /// PHASE 7: lets another engine (e.g. Microsoft Agent Framework) run the think -> act -> observe
        /// loop. It gets the same history, tools and limits, and records steps the same way. Afterwards
        /// we note where its real tool results sit in the history, so they are shrunk like ours.
        /// </summary>
        private async Task<string> RunExternalLoopAsync(List<ChatMessage> history, AgentResult result, TurnState turn, CancellationToken cancellationToken)
        {
            IReadOnlyList<ToolDefinition> tools = _tools.GetDefinitions(_context);
            AgentLoopRun run = new AgentLoopRun(this, history, result, tools, _tools, _context, _options, _llm);
            string answer = await _externalLoop.RunAsync(run, cancellationToken);

            // Find each real tool result in the history (by its call id) and note its position.
            foreach (RanToolResult ranTool in run.ToolResults)
            {
                for (int i = history.Count - 1; i > 0; i--)
                {
                    if (history[i].Role == ChatRole.Tool && history[i].ToolCallId == ranTool.ToolCallId)
                    {
                        turn.ToolResultSlots.Add(new ToolResultSlot(i, ranTool.ToolCallId, ranTool.ToolName));
                        break;
                    }
                }
            }

            if (answer == null)
                return "";
            return answer;
        }

        /// <summary>Makes one call to the LLM, records tokens and timing, and adds the reply to the history.</summary>
        private async Task<ChatMessage> AskModelAsync(int step, List<ChatMessage> history, IReadOnlyList<ToolDefinition> tools,
            AgentResult result, CancellationToken cancellationToken)
        {
            Stopwatch llmTimer = Stopwatch.StartNew();
            List<ChatMessage> messagesToSend = TrimHistory(history);
            LlmResponse response;

            using (Activity llmSpan = AgentTelemetry.ActivitySource.StartActivity(AgentTelemetry.ChatOperation + " " + _llm.Model))
            {
                if (llmSpan != null)
                {
                    llmSpan.SetTag(AgentTelemetry.OperationNameTag, AgentTelemetry.ChatOperation);
                    llmSpan.SetTag(AgentTelemetry.RequestModelTag, _llm.Model);
                }

                LlmRequest request = new LlmRequest(messagesToSend, tools, _options.Temperature);
                response = await _llm.CompleteAsync(request, cancellationToken);

                if (llmSpan != null)
                {
                    llmSpan.SetTag(AgentTelemetry.InputTokensTag, response.InputTokens);
                    llmSpan.SetTag(AgentTelemetry.OutputTokensTag, response.OutputTokens);
                }
            }

            double durationMs = Math.Round(llmTimer.Elapsed.TotalMilliseconds, 1);

            // TOKENS are the word-pieces LLM providers count and charge for. Track them.
            result.InputTokens += response.InputTokens;
            result.OutputTokens += response.OutputTokens;
            RecordLlmMetrics(response, durationMs);
            history.Add(response.Message);

            // Record what the model decided, so the step list explains the answer.
            IReadOnlyList<ToolCall> toolCalls = response.Message.ToolCalls;
            string decision;
            if (toolCalls != null && toolCalls.Count > 0)
                decision = "wants tool(s): " + DescribeToolCalls(toolCalls);
            else
                decision = response.Message.Content ?? "";

            AgentStep llmStep = NewStep(result, AgentStepTypes.Llm, "llm call " + step);
            llmStep.Input = messagesToSend.Count + " messages, " + tools.Count + " tools available";
            llmStep.Output = MaskForRecord(decision);
            llmStep.InputTokens = response.InputTokens;
            llmStep.OutputTokens = response.OutputTokens;
            llmStep.DurationMs = durationMs;
            return response.Message;
        }

        /// <summary>
        /// Runs every tool the model asked for, one after another, and adds each result to the history.
        /// LOOP CONTROL (BRD R11):
        ///  - a call identical to an earlier one in this answer (same tool, same arguments) is NOT run
        ///    again: the model is told to use the earlier result, and the step is marked IsRepeat;
        ///  - after MaxToolCallsPerRun calls, the rest are not run and the loop stops.
        /// Every call still gets a tool result in the history, because the API rejects a tool
        /// request without an answer.
        /// </summary>
        private async Task RunRequestedToolsAsync(IReadOnlyList<ToolCall> toolCalls, List<ChatMessage> history, AgentResult result,
            TurnState turn, CancellationToken cancellationToken)
        {
            foreach (ToolCall call in toolCalls)
            {
                AgentStep toolStep;

                // ---- Limit reached: answer the call without running it ----
                if (turn.ToolCallCount >= _options.MaxToolCallsPerRun)
                {
                    turn.HitToolCallLimit = true;
                    history.Add(ChatMessage.ToolResult(call.Id, "Not run: the limit of " + _options.MaxToolCallsPerRun + " tool calls per answer was reached."));
                    toolStep = NewStep(result, AgentStepTypes.Tool, call.Name);
                    toolStep.Input = MaskForRecord(call.ArgumentsJson);
                    toolStep.Output = "Not run: tool call limit (" + _options.MaxToolCallsPerRun + ") reached.";
                    toolStep.IsError = true;
                    continue;
                }

                turn.ToolCallCount++;

                // ---- Repeat: same tool, same arguments, in this same answer ----
                string key = ToolCallKey(call);
                if (turn.SeenCalls.Contains(key))
                {
                    history.Add(ChatMessage.ToolResult(call.Id,
                        "You already called " + call.Name + " with these arguments in this answer. Use the earlier result instead of calling it again."));
                    toolStep = NewStep(result, AgentStepTypes.Tool, call.Name);
                    toolStep.Input = MaskForRecord(call.ArgumentsJson);
                    toolStep.Output = "Repeated call: not run again, the model was told to use the earlier result.";
                    toolStep.IsRepeat = true;
                    continue;
                }

                turn.SeenCalls.Add(key);
                ToolExecutionResult toolResult = await _tools.ExecuteAsync(call, _context, cancellationToken);

                // The model gets the FULL result; the saved step gets a masked, shortened copy.
                turn.ToolResultSlots.Add(new ToolResultSlot(history.Count, call.Id, call.Name));
                history.Add(ChatMessage.ToolResult(call.Id, toolResult.Output));
                result.ToolsUsed.Add(call.Name);

                toolStep = NewStep(result, AgentStepTypes.Tool, call.Name);
                toolStep.Input = MaskForRecord(call.ArgumentsJson);
                toolStep.Output = MaskForRecord(toolResult.Output);
                toolStep.DurationMs = Math.Round(toolResult.DurationMs, 1);
                toolStep.IsError = !toolResult.Success;
            }
        }

        /// <summary>
        /// GROUNDEDNESS: are the prices and stock numbers in the answer backed by data? The evidence is
        /// this answer's full tool results plus the earlier conversation (not the system prompt, and not
        /// the answer itself). See Evals/Groundedness.cs.
        /// </summary>
        private static void CheckGroundedness(List<ChatMessage> history, AgentResult result)
        {
            List<string> evidence = new List<string>();
            int last = history.Count - 1;
            bool lastIsAnswer = last >= 0 && history[last].Role == ChatRole.Assistant && history[last].ToolCalls == null;
            for (int i = 1; i < history.Count; i++) // history[0] is the system prompt
            {
                if (i == last && lastIsAnswer)
                    continue;
                evidence.Add(history[i].Content);
            }

            GroundednessResult check = Groundedness.Check(result.Answer, evidence);
            result.GroundedFactCount = check.GroundedCount;
            result.MadeUpFacts.AddRange(check.MadeUp);
        }

        /// <summary>Replaces this answer's tool results in the history with short stubs that keep the ids.</summary>
        private void ShrinkToolResults(List<ChatMessage> history, TurnState turn)
        {
            foreach (ToolResultSlot slot in turn.ToolResultSlots)
            {
                ChatMessage original = history[slot.HistoryIndex];
                string stub = ToolResultSummariser.Summarise(_tools.Find(slot.ToolName), slot.ToolName, original.Content);
                history[slot.HistoryIndex] = ChatMessage.ToolResult(slot.ToolCallId, stub);
            }
        }

        /// <summary>"tool name + arguments with the keys sorted", so {"a":1,"b":2} and {"b":2,"a":1} count as the same call.</summary>
        internal static string ToolCallKey(ToolCall call)
        {
            string name = (call.Name ?? "").ToLowerInvariant();
            string json = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    StringBuilder builder = new StringBuilder();
                    WriteSorted(document.RootElement, builder);
                    return name + " " + builder;
                }
            }
            catch (JsonException)
            {
                return name + " " + json.Trim();
            }
        }

        /// <summary>Writes JSON with object keys in alphabetical order (lower-cased), and text values lower-cased.</summary>
        private static void WriteSorted(JsonElement element, StringBuilder builder)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    List<JsonProperty> properties = new List<JsonProperty>(element.EnumerateObject());
                    properties.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    builder.Append('{');
                    for (int i = 0; i < properties.Count; i++)
                    {
                        if (i > 0)
                            builder.Append(',');
                        builder.Append(properties[i].Name.ToLowerInvariant()).Append(':');
                        WriteSorted(properties[i].Value, builder);
                    }
                    builder.Append('}');
                    break;

                case JsonValueKind.Array:
                    builder.Append('[');
                    int index = 0;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        if (index++ > 0)
                            builder.Append(',');
                        WriteSorted(item, builder);
                    }
                    builder.Append(']');
                    break;

                case JsonValueKind.String:
                    builder.Append('"').Append(element.GetString().Trim().ToLowerInvariant()).Append('"');
                    break;

                default:
                    builder.Append(element.GetRawText());
                    break;
            }
        }

        /// <summary>Runs one guardrail pipeline and records its checks and a summary step.</summary>
        private GuardrailPipelineResult RunGuardrails(GuardrailPipeline pipeline, string text, AgentResult result)
        {
            GuardrailPipelineResult check = pipeline.Run(text);
            result.GuardrailChecks.AddRange(check.Checks);

            // One step per stage, e.g. "input-length: Passed, prompt-injection: Passed, pii-redaction: Modified".
            // Only outcomes are saved here, never the raw text, so no personal data ends up in the record.
            List<string> outcomes = new List<string>();
            double totalMs = 0;
            foreach (GuardrailCheck item in check.Checks)
            {
                outcomes.Add(item.Name + ": " + item.Outcome);
                totalMs += item.DurationMs;
            }

            AgentStep step = NewStep(result, AgentStepTypes.Guardrail, pipeline.Stage + " guardrails");
            step.Output = string.Join(", ", outcomes);
            step.DurationMs = Math.Round(totalMs, 3);
            return check;
        }

        /// <summary>Replaces the answer with the safe fallback and records why it was blocked.</summary>
        private void Block(AgentResult result, string blockedBy, string reason, string stage)
        {
            result.Blocked = true;
            result.BlockedBy = blockedBy;
            result.BlockedReason = reason;
            result.Answer = _options.FallbackAnswer;

            // When the INPUT is blocked, the output guardrails never run. Record them as Skipped
            // so the UI still shows every check.
            if (stage == GuardrailStages.Input)
                result.GuardrailChecks.AddRange(_guardrails.Output.Skip("The input was blocked, so no answer was written."));

            AgentTelemetry.GuardrailBlocks.Add(1,
                new KeyValuePair<string, object>("stage", stage),
                new KeyValuePair<string, object>("guardrail", blockedBy));
            _logger.LogWarning("Guardrail {Guardrail} blocked {Stage}: {Reason}", blockedBy, stage, reason);
        }

        /// <summary>Saves the run record. A store failure is logged and never breaks the answer.</summary>
        private async Task SaveRunRecordAsync(AgentResult result)
        {
            try
            {
                // CancellationToken.None: the answer is ready, so save it even if the caller has gone.
                await _runStore.SaveAsync(AgentRunRecord.FromResult(result, _context), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save agent run {RunId}", result.RunId);
            }
        }

        /// <summary>Writes the final timing, trace tags and one structured log line for the request.</summary>
        private void RecordFinishedRun(AgentResult result, Activity activity)
        {
            AgentTelemetry.Duration.Record(result.DurationMs);

            if (activity != null)
            {
                activity.SetTag(AgentTelemetry.InputTokensTag, result.InputTokens);
                activity.SetTag(AgentTelemetry.OutputTokensTag, result.OutputTokens);
                activity.SetTag("agent.tools_used", string.Join(",", result.ToolsUsed));
                activity.SetTag("agent.blocked", result.Blocked);
            }

            // A structured log line: in Azure or NLog you can filter and chart on every {field}.
            _logger.LogInformation(
                "Agent run {RunId} answered session {SessionId} in {DurationMs} ms. Tools: [{Tools}], tokens in/out: {In}/{Out}, blocked: {Blocked}",
                result.RunId, result.SessionId, result.DurationMs, string.Join(",", result.ToolsUsed), result.InputTokens, result.OutputTokens, result.Blocked);
        }

        /// <summary>Records the standard GenAI metrics for one LLM call.</summary>
        private void RecordLlmMetrics(LlmResponse response, double durationMs)
        {
            KeyValuePair<string, object> operation = new KeyValuePair<string, object>(AgentTelemetry.OperationNameTag, AgentTelemetry.ChatOperation);
            KeyValuePair<string, object> model = new KeyValuePair<string, object>(AgentTelemetry.RequestModelTag, _llm.Model);

            AgentTelemetry.TokenUsage.Record(response.InputTokens, operation, model,
                new KeyValuePair<string, object>(AgentTelemetry.TokenTypeTag, "input"));
            AgentTelemetry.TokenUsage.Record(response.OutputTokens, operation, model,
                new KeyValuePair<string, object>(AgentTelemetry.TokenTypeTag, "output"));
            AgentTelemetry.OperationDuration.Record(durationMs / 1000.0, operation, model);
        }

        /// <summary>Adds a new, numbered step to the result and returns it so the caller can fill it in.</summary>
        internal static AgentStep NewStep(AgentResult result, string type, string name)
        {
            AgentStep step = new AgentStep();
            step.Index = result.Steps.Count + 1;
            step.Type = type;
            step.Name = name;
            result.Steps.Add(step);
            return step;
        }

        /// <summary>Turns tool calls into readable text like: search_products({"type":"Hats"}).</summary>
        internal static string DescribeToolCalls(IReadOnlyList<ToolCall> toolCalls)
        {
            List<string> parts = new List<string>();
            foreach (ToolCall call in toolCalls)
                parts.Add(call.Name + "(" + call.ArgumentsJson + ")");

            return string.Join(", ", parts);
        }

        /// <summary>Masks personal data and shortens long text before it is saved in a run record.</summary>
        internal static string MaskForRecord(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            string masked = PiiRedactionGuardrail.Redact(text);
            if (masked.Length <= MaxStepTextLength)
                return masked;

            return masked.Substring(0, MaxStepTextLength) + "... (cut, " + masked.Length + " characters in total)";
        }

        /// <summary>A conversation's owner must match the current user. Two anonymous users count as the same "nobody".</summary>
        private static bool IsSameOwner(string ownerEmail, string currentEmail)
        {
            bool ownerIsAnonymous = string.IsNullOrWhiteSpace(ownerEmail);
            bool currentIsAnonymous = string.IsNullOrWhiteSpace(currentEmail);

            if (ownerIsAnonymous || currentIsAnonymous)
                return ownerIsAnonymous && currentIsAnonymous;

            return string.Equals(ownerEmail, currentEmail, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Uses the caller's session id when it is sensible, otherwise makes a new one.</summary>
        private static string NormaliseSessionId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > MaxSessionIdLength)
                return Guid.NewGuid().ToString("N");

            return sessionId.Trim();
        }

        /// <summary>
        /// Keeps the system prompt plus the most recent messages. We always start the kept part at a
        /// USER message, because cutting between a tool request and its result confuses the model
        /// (and the OpenAI API rejects it).
        /// </summary>
        internal List<ChatMessage> TrimHistory(List<ChatMessage> history)
        {
            if (history.Count <= _options.MaxHistoryMessages)
                return history;

            // Move the starting point forward until it lands on a user message.
            int start = history.Count - _options.MaxHistoryMessages;
            while (start < history.Count && history[start].Role != ChatRole.User)
                start++;

            // No user message after that point means the CURRENT turn alone is longer than the limit
            // (many tool calls). Then keep the whole current turn, starting at its user message.
            if (start == history.Count)
            {
                start = history.Count - 1;
                while (start > 1 && history[start].Role != ChatRole.User)
                    start--;
            }

            List<ChatMessage> trimmed = new List<ChatMessage>();
            trimmed.Add(history[0]); // history[0] is always the system prompt
            for (int i = start; i < history.Count; i++)
                trimmed.Add(history[i]);

            return trimmed;
        }

        /// <summary>What the loop keeps track of while answering ONE message.</summary>
        private sealed class TurnState
        {
            public TurnState()
            {
                SeenCalls = new HashSet<string>(StringComparer.Ordinal);
                ToolResultSlots = new List<ToolResultSlot>();
            }

            /// <summary>Tool calls asked for so far in this answer (repeats included).</summary>
            public int ToolCallCount { get; set; }

            /// <summary>True once a call was refused because of MaxToolCallsPerRun.</summary>
            public bool HitToolCallLimit { get; set; }

            /// <summary>ToolCallKey of every call that really ran.</summary>
            public HashSet<string> SeenCalls { get; }

            /// <summary>Where this answer's tool results sit in the history, so they can be shrunk later.</summary>
            public List<ToolResultSlot> ToolResultSlots { get; }
        }

        /// <summary>One tool result in the history: its position, call id and tool name.</summary>
        private sealed class ToolResultSlot
        {
            public ToolResultSlot(int historyIndex, string toolCallId, string toolName)
            {
                HistoryIndex = historyIndex;
                ToolCallId = toolCallId;
                ToolName = toolName;
            }

            public int HistoryIndex { get; }
            public string ToolCallId { get; }
            public string ToolName { get; }
        }
    }
}
