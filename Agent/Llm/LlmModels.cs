using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Agent.Llm
{
    // ---------------------------------------------------------------------------
    // These small types describe a conversation with a Large Language Model (LLM).
    // Every chat-style LLM API (OpenAI, Azure OpenAI, Anthropic, Ollama...) uses
    // roughly the same idea: a list of messages, each with a "role".
    // ---------------------------------------------------------------------------

    /// <summary>Who wrote a message.</summary>
    public enum ChatRole
    {
        System,     // Instructions for the model ("You are the shop assistant...")
        User,       // What the human typed
        Assistant,  // What the model replied (text, or a request to call a tool)
        Tool        // The result of running a tool, sent back to the model
    }

    /// <summary>
    /// The model asking us to run a tool. Example: Name = "search_products",
    /// ArgumentsJson = {"type":"Hats"}. The model never runs code itself;
    /// it only ASKS, and our code decides whether and how to run it.
    /// </summary>
    public sealed class ToolCall
    {
        /// <summary>Creates a tool call.</summary>
        public ToolCall(string id, string name, string argumentsJson, string extraContentJson = null)
        {
            Id = id;
            Name = name;
            ArgumentsJson = argumentsJson;
            ExtraContentJson = extraContentJson;
        }

        /// <summary>
        /// Provider-specific data that must be sent back unchanged with the conversation, as raw JSON.
        /// Gemini puts its "thought_signature" here and refuses the next call without it.
        /// Null for OpenAI.
        /// </summary>
        public string ExtraContentJson { get; }

        /// <summary>Unique id. The tool's result must quote it so the model can match them up.</summary>
        public string Id { get; }

        /// <summary>Which tool the model wants, e.g. "search_products".</summary>
        public string Name { get; }

        /// <summary>The arguments as raw JSON text, exactly as the model wrote them.</summary>
        public string ArgumentsJson { get; }
    }

    /// <summary>One message in the conversation. Messages never change after they are created.</summary>
    public sealed class ChatMessage
    {
        /// <summary>Creates a message. The factory methods below are easier to read, so prefer those.</summary>
        public ChatMessage(ChatRole role, string content, IReadOnlyList<ToolCall> toolCalls, string toolCallId)
        {
            Role = role;
            Content = content;
            ToolCalls = toolCalls;
            ToolCallId = toolCallId;
        }

        /// <summary>Who wrote it.</summary>
        public ChatRole Role { get; }

        /// <summary>The text. Can be null when the assistant only asks for tools.</summary>
        public string Content { get; }

        /// <summary>Only set when the assistant wants to call one or more tools.</summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; }

        /// <summary>Only set on Tool messages: which tool call this result answers.</summary>
        public string ToolCallId { get; }

        // Small helper "factory" methods so the rest of the code reads nicely.

        /// <summary>Instructions for the model.</summary>
        public static ChatMessage SystemMessage(string text)
        {
            return new ChatMessage(ChatRole.System, text, null, null);
        }

        /// <summary>What the user typed.</summary>
        public static ChatMessage UserMessage(string text)
        {
            return new ChatMessage(ChatRole.User, text, null, null);
        }

        /// <summary>A plain text reply from the model.</summary>
        public static ChatMessage AssistantMessage(string text)
        {
            return new ChatMessage(ChatRole.Assistant, text, null, null);
        }

        /// <summary>A reply where the model asks us to run tools (it may also include some text).</summary>
        public static ChatMessage AssistantToolCalls(IReadOnlyList<ToolCall> calls, string text = null)
        {
            return new ChatMessage(ChatRole.Assistant, text, calls, null);
        }

        /// <summary>The result of running one tool, sent back to the model.</summary>
        public static ChatMessage ToolResult(string toolCallId, string result)
        {
            return new ChatMessage(ChatRole.Tool, result, null, toolCallId);
        }
    }

    /// <summary>
    /// Describes a tool to the model: its name, what it does, and what inputs it takes
    /// (as a JSON Schema). The model reads the description to decide WHEN to use it,
    /// so a clear description matters as much as the code.
    /// </summary>
    public sealed class ToolDefinition
    {
        /// <summary>Creates a tool description.</summary>
        public ToolDefinition(string name, string description, JsonObject parametersSchema)
        {
            Name = name;
            Description = description;
            ParametersSchema = parametersSchema;
        }

        /// <summary>The tool's name, e.g. "search_products".</summary>
        public string Name { get; }

        /// <summary>Plain-English text telling the model when to use the tool.</summary>
        public string Description { get; }

        /// <summary>JSON Schema describing the arguments.</summary>
        public JsonObject ParametersSchema { get; }
    }

    /// <summary>Everything we send to the model in one call.</summary>
    public sealed class LlmRequest
    {
        /// <summary>Creates a request. Leave temperature null to let the model use its own default.</summary>
        public LlmRequest(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, double? temperature)
        {
            Messages = messages;
            Tools = tools ?? new List<ToolDefinition>();
            Temperature = temperature;
        }

        /// <summary>The conversation so far, starting with the system prompt.</summary>
        public IReadOnlyList<ChatMessage> Messages { get; }

        /// <summary>The tools the model may ask for.</summary>
        public IReadOnlyList<ToolDefinition> Tools { get; }

        /// <summary>0 = predictable, 1 = creative. Null = not sent (gpt-5 reasoning models reject it).</summary>
        public double? Temperature { get; }
    }

    /// <summary>
    /// What comes back: the model's message plus how many "tokens" (roughly word-pieces)
    /// were used. Tokens = cost, so we track them carefully.
    /// </summary>
    public sealed class LlmResponse
    {
        /// <summary>Creates a response.</summary>
        public LlmResponse(ChatMessage message, int inputTokens, int outputTokens)
        {
            Message = message;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }

        /// <summary>The model's reply: text, or a request to run tools.</summary>
        public ChatMessage Message { get; }

        /// <summary>Tokens we sent.</summary>
        public int InputTokens { get; }

        /// <summary>Tokens the model wrote.</summary>
        public int OutputTokens { get; }
    }

    /// <summary>
    /// How an LLM call failed, so the UI can tell the user WHY the assistant could not answer
    /// (quota used up, subscription/key problem, provider down) instead of a generic error.
    /// </summary>
    public enum LlmFailureKind
    {
        /// <summary>Anything we could not place in a more specific bucket.</summary>
        Other = 0,

        /// <summary>HTTP 429 or an "exhausted/quota" message: a free-tier or rate limit was hit.</summary>
        QuotaExceeded,

        /// <summary>HTTP 401/402/403: the API key is missing, invalid, expired, or has no active subscription.</summary>
        Authentication,

        /// <summary>HTTP 5xx or a network failure: the provider is down or unreachable right now.</summary>
        ServiceUnavailable
    }

    /// <summary>Thrown when the LLM provider fails in a way retrying won't fix.</summary>
    public sealed class LlmException : Exception
    {
        /// <summary>Creates the exception with a helpful message (kind defaults to Other).</summary>
        public LlmException(string message) : this(message, LlmFailureKind.Other, null)
        {
        }

        /// <summary>Creates the exception with a technical message, a kind, and a message safe to show the user.</summary>
        public LlmException(string message, LlmFailureKind kind, string userMessage) : base(message)
        {
            Kind = kind;
            UserMessage = string.IsNullOrWhiteSpace(userMessage)
                ? "The AI service is currently unavailable. Please try again in a moment."
                : userMessage;
        }

        /// <summary>Why the call failed, for the UI to react to.</summary>
        public LlmFailureKind Kind { get; }

        /// <summary>A short, safe sentence to show the user (never contains keys or stack traces).</summary>
        public string UserMessage { get; }
    }
}
