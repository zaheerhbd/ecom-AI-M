using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Tools;
using Microsoft.Extensions.AI;

namespace Agent.Framework
{
    /// <summary>
    /// The TOOL ADAPTER: shows one of our tools (search_products, get_my_orders, ...) to Agent
    /// Framework as an AIFunction, Microsoft's "a function the model may call".
    ///
    /// The name, description and JSON schema are exactly the ones the custom engine sends. Running the
    /// tool goes through OUR ToolRegistry, so the same rules apply: sign-in tools refuse anonymous
    /// users, every tool has a timeout, and problems come back as "Error: ..." text.
    ///
    /// The custom loop's LOOP CONTROL (BRD R11) lives here too, with the same messages:
    ///  - a call identical to an earlier one in this answer is not run again (step marked IsRepeat);
    ///  - after MaxToolCallsPerRun calls, the rest are not run, and LlmChatClient then ends the answer.
    /// </summary>
    internal sealed class ShopToolFunction : AIFunction
    {
        private readonly ToolDefinition _definition;
        private readonly AgentLoopRun _run;
        private readonly FrameworkTurn _turn;
        private readonly JsonElement _schema;

        public ShopToolFunction(ToolDefinition definition, AgentLoopRun run, FrameworkTurn turn)
        {
            _definition = definition;
            _run = run;
            _turn = turn;
            // The JSON schema of the arguments, in the form the framework wants (a JsonElement).
            string schemaJson = "{\"type\":\"object\"}"; // a tool without parameters
            if (definition.ParametersSchema != null)
                schemaJson = definition.ParametersSchema.ToJsonString();

            using (JsonDocument document = JsonDocument.Parse(schemaJson))
            {
                _schema = document.RootElement.Clone(); // Clone: the document is disposed after this block
            }
        }

        public override string Name
        {
            get { return _definition.Name; }
        }

        public override string Description
        {
            get { return _definition.Description; }
        }

        public override JsonElement JsonSchema
        {
            get { return _schema; }
        }

        /// <summary>Called by the framework when the model asks for this tool. Returns text for the model.</summary>
        protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            // The framework tells us which request this is; it carries the call id and the raw arguments.
            FunctionCallContent request = null;
            FunctionInvocationContext invocation = FunctionInvokingChatClient.CurrentContext;
            if (invocation != null)
                request = invocation.CallContent;

            // The call id links the tool result to the request. Make one up only if it is missing.
            string callId = Guid.NewGuid().ToString("N");
            if (request != null && !string.IsNullOrEmpty(request.CallId))
                callId = request.CallId;

            // The arguments exactly as the model wrote them (needed for repeat detection and the step list).
            string argumentsJson;
            if (request != null)
                argumentsJson = MessageMapper.RawArguments(request);
            else
                argumentsJson = JsonSerializer.Serialize(arguments);

            ToolCall call = new ToolCall(callId, Name, argumentsJson);

            AgentStep step;
            int limit = _run.Options.MaxToolCallsPerRun;

            // ---- Limit reached: answer the call without running it ----
            if (_turn.ToolCallCount >= limit)
            {
                _turn.HitToolCallLimit = true;
                step = _run.AddStep(AgentStepTypes.Tool, Name);
                step.Input = AgentLoopRun.MaskForRecord(argumentsJson);
                step.Output = "Not run: tool call limit (" + limit + ") reached.";
                step.IsError = true;
                return "Not run: the limit of " + limit + " tool calls per answer was reached.";
            }

            _turn.ToolCallCount++;

            // ---- Repeat: same tool, same arguments, in this same answer ----
            string key = AgentLoopRun.ToolCallKey(call);
            if (_turn.SeenCalls.Contains(key))
            {
                step = _run.AddStep(AgentStepTypes.Tool, Name);
                step.Input = AgentLoopRun.MaskForRecord(argumentsJson);
                step.Output = "Repeated call: not run again, the model was told to use the earlier result.";
                step.IsRepeat = true;
                return "You already called " + Name + " with these arguments in this answer. Use the earlier result instead of calling it again.";
            }

            _turn.SeenCalls.Add(key);

            // ---- Run it through our registry (sign-in rule, timeout, errors as text) ----
            ToolExecutionResult result = await _run.Registry.ExecuteAsync(call, _run.Context, cancellationToken);
            _run.MarkToolResult(callId, Name);
            _run.Result.ToolsUsed.Add(Name);

            step = _run.AddStep(AgentStepTypes.Tool, Name);
            step.Input = AgentLoopRun.MaskForRecord(argumentsJson);
            step.Output = AgentLoopRun.MaskForRecord(result.Output);
            step.DurationMs = Math.Round(result.DurationMs, 1);
            step.IsError = !result.Success;

            // The model gets the FULL result; the saved step has a masked, shortened copy.
            return result.Output;
        }
    }
}
