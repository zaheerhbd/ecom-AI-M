using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.AI;
using AgentChatMessage = Agent.Llm.ChatMessage;
using AgentChatRole = Agent.Llm.ChatRole;
using AgentToolCall = Agent.Llm.ToolCall;

namespace Agent.Framework
{
    /// <summary>
    /// Translates chat messages between OUR format (Agent.Llm.ChatMessage, used by memory, the run
    /// records and our LLM client) and MICROSOFT's format (Microsoft.Extensions.AI.ChatMessage, used
    /// by Agent Framework).
    ///
    /// Both describe the same thing: a list of messages with roles, where the assistant can ask for
    /// tools and tool results come back. The differences:
    ///  - ours has one ToolCall list per assistant message and ONE tool result per message;
    ///  - Microsoft's uses "contents": TextContent, FunctionCallContent and FunctionResultContent,
    ///    and one Tool message may hold several results.
    ///
    /// Two extra values ride along on each FunctionCallContent (in AdditionalProperties), so nothing
    /// is lost on the way round:
    ///  - the arguments exactly as the model wrote them (for repeat detection and the step list);
    ///  - Gemini's "extra_content" (its thought signature), which Gemini needs back on the next call.
    /// </summary>
    internal static class MessageMapper
    {
        /// <summary>Key for the raw JSON arguments on a FunctionCallContent.</summary>
        internal const string RawArgumentsKey = "agent.raw_arguments";

        /// <summary>Key for provider extra data (Gemini thought signature) on a FunctionCallContent.</summary>
        internal const string ExtraContentKey = "agent.extra_content";

        // ------------------------------------------------------------------ ours -> Microsoft

        /// <summary>Our conversation as Microsoft messages.</summary>
        public static List<ChatMessage> ToFramework(IEnumerable<AgentChatMessage> messages)
        {
            List<ChatMessage> result = new List<ChatMessage>();
            foreach (AgentChatMessage message in messages)
                result.Add(ToFramework(message));

            return result;
        }

        /// <summary>One of our messages as a Microsoft message.</summary>
        public static ChatMessage ToFramework(AgentChatMessage message)
        {
            string text = TextOrEmpty(message.Content);

            if (message.Role == AgentChatRole.System)
                return new ChatMessage(ChatRole.System, text);

            if (message.Role == AgentChatRole.User)
                return new ChatMessage(ChatRole.User, text);

            if (message.Role == AgentChatRole.Tool)
            {
                List<AIContent> toolContents = new List<AIContent>();
                toolContents.Add(new FunctionResultContent(message.ToolCallId, text));
                return new ChatMessage(ChatRole.Tool, toolContents);
            }

            // Assistant: text, tool requests, or both.
            List<AIContent> contents = new List<AIContent>();
            if (text.Length > 0)
                contents.Add(new TextContent(text));

            if (message.ToolCalls != null)
            {
                foreach (AgentToolCall call in message.ToolCalls)
                    contents.Add(ToFunctionCall(call));
            }

            return new ChatMessage(ChatRole.Assistant, contents);
        }

        /// <summary>Our tool call as a FunctionCallContent, keeping the raw arguments and Gemini's extra data.</summary>
        public static FunctionCallContent ToFunctionCall(AgentToolCall call)
        {
            string json = call.ArgumentsJson;
            if (string.IsNullOrWhiteSpace(json))
                json = "{}";

            FunctionCallContent content = new FunctionCallContent(call.Id, call.Name, ParseArguments(json));
            content.AdditionalProperties = new AdditionalPropertiesDictionary();
            content.AdditionalProperties[RawArgumentsKey] = json;
            if (call.ExtraContentJson != null)
                content.AdditionalProperties[ExtraContentKey] = call.ExtraContentJson;

            return content;
        }

        /// <summary>
        /// JSON arguments as a dictionary. Values stay JsonElements. Bad JSON gives an empty dictionary;
        /// the raw text is still kept, so the tool sees exactly what the model sent and reports the error.
        /// </summary>
        private static Dictionary<string, object> ParseArguments(string json)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                            arguments[property.Name] = property.Value.Clone();
                    }
                }
            }
            catch (JsonException)
            {
                // Leave it empty: the raw arguments go to the tool unchanged.
            }

            return arguments;
        }

        // ------------------------------------------------------------------ Microsoft -> ours

        /// <summary>Microsoft messages as ours. One Tool message with several results becomes several of ours.</summary>
        public static List<AgentChatMessage> ToAgent(IEnumerable<ChatMessage> messages)
        {
            List<AgentChatMessage> result = new List<AgentChatMessage>();
            foreach (ChatMessage message in messages)
                AddAsAgent(message, result);

            return result;
        }

        private static void AddAsAgent(ChatMessage message, List<AgentChatMessage> result)
        {
            if (message.Role == ChatRole.System)
            {
                result.Add(AgentChatMessage.SystemMessage(message.Text));
                return;
            }

            if (message.Role == ChatRole.User)
            {
                result.Add(AgentChatMessage.UserMessage(message.Text));
                return;
            }

            if (message.Role == ChatRole.Tool)
            {
                foreach (AIContent content in message.Contents)
                {
                    FunctionResultContent toolResult = content as FunctionResultContent;
                    if (toolResult != null)
                        result.Add(AgentChatMessage.ToolResult(toolResult.CallId, ResultText(toolResult.Result)));
                }
                return;
            }

            // Assistant: collect its tool requests (if any) and its text.
            List<AgentToolCall> calls = new List<AgentToolCall>();
            foreach (AIContent content in message.Contents)
            {
                FunctionCallContent call = content as FunctionCallContent;
                if (call != null)
                    calls.Add(ToToolCall(call));
            }

            if (calls.Count > 0)
            {
                // A tool request may come with some text, or none (null).
                string textWithCalls = null;
                if (!string.IsNullOrEmpty(message.Text))
                    textWithCalls = message.Text;

                result.Add(AgentChatMessage.AssistantToolCalls(calls, textWithCalls));
            }
            else
            {
                result.Add(AgentChatMessage.AssistantMessage(TextOrEmpty(message.Text)));
            }
        }

        /// <summary>A FunctionCallContent as our ToolCall, with the raw arguments and Gemini's extra data put back.</summary>
        public static AgentToolCall ToToolCall(FunctionCallContent call)
        {
            string arguments = RawArguments(call);
            string extra = GetExtra(call, ExtraContentKey);
            return new AgentToolCall(call.CallId, call.Name, arguments, extra);
        }

        /// <summary>The arguments exactly as the model wrote them, or the parsed ones written back as JSON.</summary>
        public static string RawArguments(FunctionCallContent call)
        {
            if (call == null)
                return "{}";

            string raw = GetExtra(call, RawArgumentsKey);
            if (raw != null)
                return raw;

            if (call.Arguments == null)
                return "{}";

            return JsonSerializer.Serialize(call.Arguments);
        }

        /// <summary>One of our extra values from a FunctionCallContent, or null if it isn't there.</summary>
        private static string GetExtra(FunctionCallContent call, string key)
        {
            if (call.AdditionalProperties == null)
                return null;

            object value;
            if (call.AdditionalProperties.TryGetValue(key, out value))
                return value as string;

            return null;
        }

        /// <summary>A tool result as text: our tools return strings; anything else is written as JSON.</summary>
        private static string ResultText(object result)
        {
            if (result == null)
                return "";

            string text = result as string;
            if (text != null)
                return text;

            if (result is JsonElement)
            {
                JsonElement element = (JsonElement)result;
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();

                return element.GetRawText();
            }

            return JsonSerializer.Serialize(result);
        }

        /// <summary>The text, or "" when there is none.</summary>
        private static string TextOrEmpty(string text)
        {
            if (text == null)
                return "";

            return text;
        }
    }
}
