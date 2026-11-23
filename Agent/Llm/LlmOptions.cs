using System;

namespace Agent.Llm
{
    /// <summary>
    /// Settings for the language model, read from the "Agent:Llm" section of appsettings.json.
    /// If ApiKey is empty here, the existing "OpenAI:ApiKey" setting is used, so there is only one secret.
    /// NEVER commit a real API key to GitHub.
    /// </summary>
    public sealed class LlmOptions
    {
        /// <summary>OpenAI's address for the Chat Completions API.</summary>
        public const string OpenAiBaseUrl = "https://api.openai.com/v1";

        /// <summary>
        /// Google Gemini's OpenAI-compatible address. Gemini accepts the same Chat Completions JSON,
        /// so the same client works for both; only the address, key and model name change.
        /// </summary>
        public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";

        /// <summary>Model used for Gemini when no model is configured.</summary>
        public const string DefaultGeminiModel = "gemini-3.5-flash";

        /// <summary>Model used for OpenAI when no model is configured.</summary>
        public const string DefaultOpenAiModel = "gpt-5.4-mini";

        /// <summary>Sets the default values. appsettings.json can override any of them.</summary>
        public LlmOptions()
        {
            Provider = "Fake";
            Model = DefaultOpenAiModel;
            BaseUrl = OpenAiBaseUrl;
            ApiVersion = "2024-10-21";
            MaxRetries = 3;
            TimeoutSeconds = 60;
        }

        /// <summary>
        /// "Fake" (free, offline, the default), "Gemini" (Google, has a free tier),
        /// "OpenAI" (also works for Ollama) or "AzureOpenAI".
        /// The default is Fake so a missing setting can never cost money.
        /// </summary>
        public string Provider { get; set; }

        /// <summary>Secret key for the provider. Set it in user-secrets or an environment variable, never in a committed file.</summary>
        public string ApiKey { get; set; }

        /// <summary>Model name for OpenAI / Ollama, e.g. "gpt-5.4-mini".</summary>
        public string Model { get; set; }

        /// <summary>Change this to use any OpenAI-compatible server, e.g. Ollama at http://localhost:11434/v1</summary>
        public string BaseUrl { get; set; }

        // --- Azure OpenAI only ---

        /// <summary>Azure only, e.g. https://my-resource.openai.azure.com</summary>
        public string Endpoint { get; set; }

        /// <summary>Azure only: the name you gave your model deployment.</summary>
        public string Deployment { get; set; }

        /// <summary>Azure only: which version of the REST API to call.</summary>
        public string ApiVersion { get; set; }

        // --- Reliability ---

        /// <summary>How many times to retry when the provider is busy (HTTP 429) or has a server error (5xx).</summary>
        public int MaxRetries { get; set; }

        /// <summary>Give up on one HTTP call after this many seconds.</summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>True when using the offline fake model.</summary>
        public bool IsFake
        {
            get { return string.Equals(Provider, "Fake", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>True when using Google Gemini (through its OpenAI-compatible API).</summary>
        public bool IsGemini
        {
            get { return string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>True when using Azure OpenAI.</summary>
        public bool IsAzure
        {
            get { return string.Equals(Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase); }
        }
    }
}
