using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Observability;

namespace Agent.Tools
{
    /// <summary>The outcome of running one tool.</summary>
    public sealed class ToolExecutionResult
    {
        /// <summary>Creates a result.</summary>
        public ToolExecutionResult(string output, bool success, double durationMs)
        {
            Output = output;
            Success = success;
            DurationMs = durationMs;
        }

        /// <summary>The text given back to the model (a result or an error message).</summary>
        public string Output { get; }

        /// <summary>False if the tool returned an error.</summary>
        public bool Success { get; }

        /// <summary>How long the tool took, in milliseconds.</summary>
        public double DurationMs { get; }
    }

    /// <summary>
    /// Keeps the list of tools and runs them SAFELY:
    ///  - unknown tool name       -> friendly error text (models sometimes invent tool names!)
    ///  - sign-in tool, anonymous -> hidden from the model, and refused if it is called anyway
    ///  - bad JSON arguments      -> friendly error text
    ///  - tool throws             -> friendly error text, the agent keeps going
    ///  - tool is too slow        -> cancelled after a timeout
    /// The model sees the error message and can try again or explain the problem to the user.
    ///
    /// One registry is created per request ("scoped"), because the shop's tools use a database
    /// context, which must not be shared between requests.
    /// </summary>
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools;
        private readonly List<ITool> _orderedTools;
        private readonly TimeSpan _timeout;

        /// <summary>Creates the registry. Each tool gets 10 seconds unless another timeout is given.</summary>
        public ToolRegistry(IEnumerable<ITool> tools, TimeSpan? timeout = null)
        {
            _tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);
            _orderedTools = new List<ITool>();

            foreach (ITool tool in tools)
            {
                // Two tools with the same name would confuse the model, so fail loudly at startup.
                if (_tools.ContainsKey(tool.Name))
                    throw new InvalidOperationException("Two tools are registered with the name '" + tool.Name + "'.");

                _tools.Add(tool.Name, tool);
                _orderedTools.Add(tool);
            }

            _timeout = timeout ?? TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// The tool descriptions to send to the model for this user. Tools marked IRequiresSignIn
        /// are left out for anonymous users, so the model doesn't even know they exist.
        /// </summary>
        public IReadOnlyList<ToolDefinition> GetDefinitions(AgentRequestContext context)
        {
            List<ToolDefinition> definitions = new List<ToolDefinition>();
            foreach (ITool tool in _orderedTools)
            {
                if (IsAllowed(tool, context))
                    definitions.Add(new ToolDefinition(tool.Name, tool.Description, tool.ParametersSchema));
            }

            return definitions;
        }

        /// <summary>The tool with this name, or null if there is none.</summary>
        public ITool Find(string name)
        {
            ITool tool;
            return name != null && _tools.TryGetValue(name, out tool) ? tool : null;
        }

        /// <summary>How long a tool may run before it is cancelled.</summary>
        public TimeSpan Timeout
        {
            get { return _timeout; }
        }

        /// <summary>Runs the tool the model asked for. Never throws for tool problems; returns error text instead.</summary>
        public async Task<ToolExecutionResult> ExecuteAsync(ToolCall call, AgentRequestContext context, CancellationToken cancellationToken)
        {
            Stopwatch timer = Stopwatch.StartNew();

            // OBSERVABILITY: each tool run becomes a "span" inside the request's trace.
            using (Activity span = AgentTelemetry.ActivitySource.StartActivity(AgentTelemetry.ExecuteToolOperation + " " + call.Name))
            {
                if (span != null)
                {
                    span.SetTag(AgentTelemetry.OperationNameTag, AgentTelemetry.ExecuteToolOperation);
                    span.SetTag(AgentTelemetry.ToolNameTag, call.Name);
                    span.SetTag(AgentTelemetry.ToolCallIdTag, call.Id);
                }

                string output = await RunToolSafelyAsync(call, context, cancellationToken);
                bool success = !output.StartsWith("Error", StringComparison.OrdinalIgnoreCase);

                AgentTelemetry.ToolCalls.Add(1,
                    new KeyValuePair<string, object>("tool", call.Name),
                    new KeyValuePair<string, object>("success", success));

                if (span != null && !success)
                    span.SetStatus(ActivityStatusCode.Error, output);

                return new ToolExecutionResult(output, success, timer.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>Finds the tool, checks sign-in, parses the arguments and runs it with a timeout, turning every problem into error text.</summary>
        private async Task<string> RunToolSafelyAsync(ToolCall call, AgentRequestContext context, CancellationToken cancellationToken)
        {
            // Unknown tool. Only list the tools this user may use, so hidden tools stay hidden.
            ITool tool;
            if (!_tools.TryGetValue(call.Name, out tool))
                return "Error: there is no tool called '" + call.Name + "'. Available tools: " + string.Join(", ", GetAllowedNames(context)) + ".";

            // Second line of defence: the tool was hidden, but the model called it anyway.
            if (!IsAllowed(tool, context))
                return "Error: '" + call.Name + "' needs the user to be signed in. Ask the user to sign in first.";

            try
            {
                string json = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson;
                using (JsonDocument document = JsonDocument.Parse(json))
                // Linked token: cancels if the caller cancels OR if our timeout expires.
                using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutSource.CancelAfter(_timeout);
                    string output = await tool.ExecuteAsync(document.RootElement.Clone(), context, timeoutSource.Token);
                    return output ?? "";
                }
            }
            catch (JsonException)
            {
                return "Error: the tool arguments were not valid JSON.";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return "Error: the tool timed out after " + _timeout.TotalSeconds + " seconds.";
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return "Error: the tool failed (" + ex.GetType().Name + ").";
            }
        }

        /// <summary>True if this user may use the tool (sign-in tools need a signed-in user).</summary>
        private static bool IsAllowed(ITool tool, AgentRequestContext context)
        {
            if (tool is IRequiresSignIn)
                return context != null && context.IsSignedIn;

            return true;
        }

        /// <summary>The names of the tools this user may use.</summary>
        private List<string> GetAllowedNames(AgentRequestContext context)
        {
            List<string> names = new List<string>();
            foreach (ITool tool in _orderedTools)
            {
                if (IsAllowed(tool, context))
                    names.Add(tool.Name);
            }

            return names;
        }
    }
}
