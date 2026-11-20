using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Llm
{
    /// <summary>
    /// A pretend LLM. It is NOT intelligent, and it costs nothing.
    ///
    /// Why have it?
    ///  1. You can run the whole app with no API key and no cost.
    ///  2. Tests get the same result every time ("deterministic").
    ///  3. It shows that the agent loop does not care WHO answers, as long as the
    ///     ILlmClient contract is respected.
    ///
    /// It works in two ways:
    ///  - SCRIPTED: tests queue up exact replies (EnqueueAnswer, EnqueueToolCall). Each call takes the next one.
    ///  - RULES (when the script is empty): after a tool ran, it repeats the tool results; if the user
    ///    mentions a tool by its exact name (e.g. "search_products"), it calls that tool with no arguments;
    ///    otherwise it says it is the offline fake model.
    /// It also remembers every request, so tests can check what the agent sent.
    /// </summary>
    public sealed class FakeLlmClient : ILlmClient
    {
        private readonly object _lock = new object();
        private readonly Queue<LlmResponse> _script = new Queue<LlmResponse>();
        private readonly List<LlmRequest> _requests = new List<LlmRequest>();

        /// <summary>Friendly name shown in logs.</summary>
        public string Name
        {
            get { return "Fake (offline rules, not a real model)"; }
        }

        /// <summary>The model name saved with each run.</summary>
        public string Model
        {
            get { return "fake-model"; }
        }

        /// <summary>A copy of every request received so far, oldest first.</summary>
        public IReadOnlyList<LlmRequest> Requests
        {
            get
            {
                lock (_lock)
                {
                    return new List<LlmRequest>(_requests);
                }
            }
        }

        /// <summary>Queues an exact reply for the next call.</summary>
        public void EnqueueResponse(LlmResponse response)
        {
            lock (_lock)
            {
                _script.Enqueue(response);
            }
        }

        /// <summary>Queues a plain text answer for the next call.</summary>
        public void EnqueueAnswer(string text, int inputTokens = 10, int outputTokens = 5)
        {
            EnqueueResponse(new LlmResponse(ChatMessage.AssistantMessage(text), inputTokens, outputTokens));
        }

        /// <summary>Queues a "please run this tool" reply for the next call.</summary>
        public void EnqueueToolCall(string toolName, string argumentsJson, int inputTokens = 10, int outputTokens = 5)
        {
            ToolCall call = new ToolCall(NewCallId(), toolName, argumentsJson);
            EnqueueResponse(new LlmResponse(ChatMessage.AssistantToolCalls(new List<ToolCall> { call }), inputTokens, outputTokens));
        }

        /// <summary>Returns the next scripted reply, or follows the simple rules when the script is empty.</summary>
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Remember a COPY of the request: the agent keeps adding to its own message list afterwards.
            LlmRequest copy = new LlmRequest(new List<ChatMessage>(request.Messages), new List<ToolDefinition>(request.Tools), request.Temperature);

            lock (_lock)
            {
                _requests.Add(copy);
                if (_script.Count > 0)
                    return Task.FromResult(_script.Dequeue());
            }

            return Task.FromResult(ReplyByRules(request));
        }

        /// <summary>The simple rules used when no reply is scripted.</summary>
        private static LlmResponse ReplyByRules(LlmRequest request)
        {
            ChatMessage last = request.Messages[request.Messages.Count - 1];
            ChatMessage reply;

            if (last.Role == ChatRole.Tool)
            {
                // The previous turn ran tools. A real model would read the results and write a
                // natural answer. We just collect the results and repeat them.
                reply = ChatMessage.AssistantMessage("Here is what I found: " + JoinLatestToolResults(request.Messages));
            }
            else
            {
                string toolName = FindMentionedTool(last.Content ?? "", request.Tools);
                if (toolName == null)
                    reply = ChatMessage.AssistantMessage(
                        "Hi! I'm the offline fake model, so I only follow simple rules. Mention a tool by its name to see the tool loop work.");
                else
                    reply = ChatMessage.AssistantToolCalls(new List<ToolCall> { new ToolCall(NewCallId(), toolName, "{}") });
            }

            return new LlmResponse(reply, EstimateInputTokens(request.Messages), EstimateTokens(reply.Content) + 4);
        }

        /// <summary>Joins the tool results at the end of the conversation (walking backwards until a non-tool message).</summary>
        private static string JoinLatestToolResults(IReadOnlyList<ChatMessage> messages)
        {
            List<string> results = new List<string>();
            for (int i = messages.Count - 1; i >= 0 && messages[i].Role == ChatRole.Tool; i--)
                results.Insert(0, messages[i].Content); // insert at the front to keep the original order

            return string.Join(" ", results);
        }

        /// <summary>Returns the first available tool whose exact name appears in the text, or null.</summary>
        private static string FindMentionedTool(string text, IReadOnlyList<ToolDefinition> tools)
        {
            foreach (ToolDefinition tool in tools)
            {
                if (text.IndexOf(tool.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return tool.Name;
            }

            return null;
        }

        /// <summary>Real APIs report token usage. We estimate it: about 4 characters per token.</summary>
        private static int EstimateInputTokens(IReadOnlyList<ChatMessage> messages)
        {
            int tokens = 0;
            foreach (ChatMessage message in messages)
                tokens += EstimateTokens(message.Content) + 4;

            return tokens;
        }

        /// <summary>About 4 characters per token.</summary>
        private static int EstimateTokens(string text)
        {
            return text == null ? 10 : text.Length / 4;
        }

        /// <summary>A random tool-call id, like the ones real models send.</summary>
        private static string NewCallId()
        {
            return "call_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
