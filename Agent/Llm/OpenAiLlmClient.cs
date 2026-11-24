using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Llm
{
    /// <summary>
    /// Talks to a real model using the "Chat Completions" HTTP API.
    /// Works with OpenAI, Azure OpenAI, and any OpenAI-compatible server (Ollama, LM Studio...).
    ///
    /// We call the HTTP API by hand instead of using an SDK so you can SEE what is sent:
    /// it is just JSON over HTTPS. No magic.
    /// </summary>
    public sealed class OpenAiLlmClient : ILlmClient
    {
        private readonly HttpClient _http;
        private readonly LlmOptions _options;
        private readonly ILogger<OpenAiLlmClient> _logger;

        /// <summary>The HttpClient comes from IHttpClientFactory (see ServiceCollectionExtensions).</summary>
        public OpenAiLlmClient(HttpClient http, IOptions<LlmOptions> options, ILogger<OpenAiLlmClient> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>Shows which provider and model are in use.</summary>
        public string Name
        {
            get
            {
                if (_options.IsAzure)
                    return "AzureOpenAI:" + _options.Deployment;

                if (_options.IsGemini)
                    return "Gemini:" + _options.Model;

                return "OpenAI:" + _options.Model;
            }
        }

        /// <summary>The model name saved with each run.</summary>
        public string Model
        {
            get { return _options.IsAzure ? _options.Deployment : _options.Model; }
        }

        /// <summary>Sends the conversation to the model, retrying when the provider is busy.</summary>
        public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            string body = BuildRequestBody(request).ToJsonString();

            // RETRY LOOP: LLM providers are often busy (HTTP 429 "too many requests") or briefly
            // down (HTTP 5xx). Waiting a little and trying again fixes most of these.
            // We wait longer each time ("exponential backoff"): 1s, 2s, 4s...
            for (int attempt = 1; ; attempt++)
            {
                using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUrl()))
                {
                    httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    AddAuthHeader(httpRequest);

                    HttpResponseMessage response;
                    try
                    {
                        response = await _http.SendAsync(httpRequest, cancellationToken);
                    }
                    catch (HttpRequestException ex)
                    {
                        // Could not reach the provider at all (DNS, connection refused, TLS...).
                        // Retry a few times, then report it as "service unavailable".
                        if (attempt <= _options.MaxRetries)
                        {
                            TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                            _logger.LogWarning(ex, "Could not reach the LLM provider. Retry {Attempt}/{Max} in {Delay}s",
                                attempt, _options.MaxRetries, delay.TotalSeconds);
                            await Task.Delay(delay, cancellationToken);
                            continue;
                        }

                        throw new LlmException("Could not reach the LLM provider: " + ex.Message,
                            LlmFailureKind.ServiceUnavailable, UserMessageFor(LlmFailureKind.ServiceUnavailable));
                    }

                    using (response)
                    {
                        string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (response.IsSuccessStatusCode)
                            return ParseResponse(responseText);

                        int statusCode = (int)response.StatusCode;
                        bool isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || statusCode >= 500;
                        if (isTransient && attempt <= _options.MaxRetries)
                        {
                            TimeSpan delay = RetryDelay(response, attempt);
                            _logger.LogWarning("LLM returned {Status}. Retry {Attempt}/{Max} in {Delay}s",
                                statusCode, attempt, _options.MaxRetries, delay.TotalSeconds);
                            await Task.Delay(delay, cancellationToken);
                            continue;
                        }

                        // Not worth retrying (or retries are used up). Fail with a message the UI can
                        // turn into a clear reason: quota used up, key/subscription problem, or provider down.
                        LlmFailureKind kind = ClassifyFailure(response.StatusCode);
                        throw new LlmException(
                            "LLM call failed with HTTP " + statusCode + ": " + Truncate(responseText, 300),
                            kind,
                            UserMessageFor(kind));
                    }
                }
            }
        }

        /// <summary>Buckets an HTTP failure so the assistant can explain WHY it could not answer.</summary>
        private static LlmFailureKind ClassifyFailure(HttpStatusCode status)
        {
            int code = (int)status;

            if (status == HttpStatusCode.TooManyRequests) // 429: rate limit or free-tier quota
                return LlmFailureKind.QuotaExceeded;

            // 401 Unauthorized, 402 Payment Required, 403 Forbidden: missing/invalid/expired key or no subscription.
            if (status == HttpStatusCode.Unauthorized || code == 402 || status == HttpStatusCode.Forbidden)
                return LlmFailureKind.Authentication;

            if (code >= 500) // provider is down or overloaded
                return LlmFailureKind.ServiceUnavailable;

            return LlmFailureKind.Other;
        }

        /// <summary>A short, safe sentence for the user. Never includes the key or the raw provider response.</summary>
        private static string UserMessageFor(LlmFailureKind kind)
        {
            switch (kind)
            {
                case LlmFailureKind.QuotaExceeded:
                    return "The AI assistant has hit its usage limit for now (the free-tier quota may be used up). Please try again later.";
                case LlmFailureKind.Authentication:
                    return "The AI assistant is not configured correctly — its access key may be missing, expired, or without an active subscription. Please contact the site owner.";
                case LlmFailureKind.ServiceUnavailable:
                    return "The AI provider is temporarily unavailable. Please try again in a few minutes.";
                default:
                    return "The AI assistant could not complete your request right now. Please try again in a moment.";
            }
        }

        /// <summary>
        /// How long to wait before retrying. If the provider says how long (the Retry-After header,
        /// common on free tiers with a per-minute limit), wait that long, up to 60 seconds.
        /// Otherwise wait 1s, 2s, 4s...
        /// </summary>
        private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
        {
            TimeSpan backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            RetryConditionHeaderValue retryAfter = response.Headers.RetryAfter;
            if (retryAfter == null)
                return backoff;

            TimeSpan? asked = retryAfter.Delta;
            if (!asked.HasValue && retryAfter.Date.HasValue)
                asked = retryAfter.Date.Value - DateTimeOffset.UtcNow;

            if (!asked.HasValue || asked.Value <= backoff)
                return backoff;

            return asked.Value > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : asked.Value;
        }

        // ----- Building the request -------------------------------------------------

        /// <summary>Azure and OpenAI use different URL shapes for the same API.</summary>
        private string BuildUrl()
        {
            if (_options.IsAzure)
                return _options.Endpoint.TrimEnd('/') + "/openai/deployments/" + _options.Deployment
                    + "/chat/completions?api-version=" + _options.ApiVersion;

            return _options.BaseUrl.TrimEnd('/') + "/chat/completions";
        }

        /// <summary>Adds the API key. Azure and OpenAI expect it in different headers.</summary>
        private void AddAuthHeader(HttpRequestMessage request)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                return; // e.g. local Ollama needs no key

            if (_options.IsAzure)
                request.Headers.Add("api-key", _options.ApiKey);
            else
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        /// <summary>
        /// Builds the JSON body: model, messages, optional temperature and the list of tools.
        /// Internal (not private) so the tests can check what would be sent.
        /// </summary>
        internal JsonObject BuildRequestBody(LlmRequest request)
        {
            JsonArray messages = new JsonArray();
            foreach (ChatMessage message in request.Messages)
                messages.Add(ToJson(message));

            JsonObject body = new JsonObject();
            body["messages"] = messages;

            // Only send temperature when it is set. gpt-5 reasoning models return an error if it is present.
            if (request.Temperature.HasValue)
                body["temperature"] = request.Temperature.Value;

            // Azure picks the model from the deployment name in the URL, so "model" is only for OpenAI.
            if (!_options.IsAzure)
                body["model"] = _options.Model;

            // Tell the model which tools exist. It will reply with "tool_calls" when it wants one.
            if (request.Tools.Count > 0)
            {
                JsonArray tools = new JsonArray();
                foreach (ToolDefinition tool in request.Tools)
                {
                    JsonObject function = new JsonObject();
                    function["name"] = tool.Name;
                    function["description"] = tool.Description;
                    function["parameters"] = tool.ParametersSchema.DeepClone();

                    JsonObject toolJson = new JsonObject();
                    toolJson["type"] = "function";
                    toolJson["function"] = function;
                    tools.Add(toolJson);
                }

                body["tools"] = tools;
            }

            return body;
        }

        /// <summary>Turns one ChatMessage into the JSON shape the API expects.</summary>
        private static JsonObject ToJson(ChatMessage message)
        {
            JsonObject json = new JsonObject();
            json["role"] = message.Role.ToString().ToLowerInvariant(); // "system", "user", "assistant", "tool"
            json["content"] = message.Content;

            if (message.ToolCalls != null && message.ToolCalls.Count > 0)
            {
                JsonArray calls = new JsonArray();
                foreach (ToolCall call in message.ToolCalls)
                {
                    JsonObject function = new JsonObject();
                    function["name"] = call.Name;
                    function["arguments"] = call.ArgumentsJson;

                    JsonObject callJson = new JsonObject();
                    callJson["id"] = call.Id;
                    callJson["type"] = "function";
                    callJson["function"] = function;

                    // Send back what the provider gave us (Gemini's thought signature), unchanged.
                    if (!string.IsNullOrEmpty(call.ExtraContentJson))
                        callJson["extra_content"] = JsonNode.Parse(call.ExtraContentJson);

                    calls.Add(callJson);
                }

                json["tool_calls"] = calls;
            }

            if (message.ToolCallId != null)
                json["tool_call_id"] = message.ToolCallId;

            return json;
        }

        // ----- Reading the response -------------------------------------------------

        /// <summary>Reads the model's reply text, any tool calls, and the token counts.</summary>
        private static LlmResponse ParseResponse(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                JsonElement message = root.GetProperty("choices")[0].GetProperty("message");

                // The text (missing or null when the model only asks for tools).
                string content = null;
                JsonElement contentElement;
                if (message.TryGetProperty("content", out contentElement) && contentElement.ValueKind == JsonValueKind.String)
                    content = contentElement.GetString();

                // Any tool calls the model wants us to run.
                List<ToolCall> toolCalls = new List<ToolCall>();
                JsonElement calls;
                if (message.TryGetProperty("tool_calls", out calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement call in calls.EnumerateArray())
                    {
                        JsonElement function = call.GetProperty("function");

                        // Some OpenAI-compatible providers leave the id out or empty: make one up.
                        string id = null;
                        JsonElement idElement;
                        if (call.TryGetProperty("id", out idElement) && idElement.ValueKind == JsonValueKind.String)
                            id = idElement.GetString();
                        if (string.IsNullOrEmpty(id))
                            id = "call_" + Guid.NewGuid().ToString("N");

                        string name = function.GetProperty("name").GetString() ?? "";

                        // Normally a JSON string; accept a JSON object too.
                        string arguments = "{}";
                        JsonElement argumentsElement;
                        if (function.TryGetProperty("arguments", out argumentsElement))
                        {
                            if (argumentsElement.ValueKind == JsonValueKind.String)
                                arguments = argumentsElement.GetString() ?? "{}";
                            else if (argumentsElement.ValueKind == JsonValueKind.Object)
                                arguments = argumentsElement.GetRawText();
                        }
                        // Gemini: {"google": {"thought_signature": "..."}}. Kept so we can send it back.
                        string extraContent = null;
                        JsonElement extraElement;
                        if (call.TryGetProperty("extra_content", out extraElement) && extraElement.ValueKind == JsonValueKind.Object)
                            extraContent = extraElement.GetRawText();

                        toolCalls.Add(new ToolCall(id, name, arguments, extraContent));
                    }
                }

                // Token usage, which is what the provider charges for.
                int inputTokens = 0;
                int outputTokens = 0;
                JsonElement usage;
                if (root.TryGetProperty("usage", out usage))
                {
                    JsonElement count;
                    if (usage.TryGetProperty("prompt_tokens", out count))
                        inputTokens = count.GetInt32();
                    if (usage.TryGetProperty("completion_tokens", out count))
                        outputTokens = count.GetInt32();
                }

                ChatMessage chatMessage;
                if (toolCalls.Count > 0)
                    chatMessage = ChatMessage.AssistantToolCalls(toolCalls, content);
                else
                    chatMessage = ChatMessage.AssistantMessage(content ?? "");

                return new LlmResponse(chatMessage, inputTokens, outputTokens);
            }
        }

        /// <summary>Shortens long text so error messages stay readable.</summary>
        private static string Truncate(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
    }
}
