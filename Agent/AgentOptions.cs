using System;

namespace Agent
{
    /// <summary>
    /// Settings for the agent, read from the "Agent" section of appsettings.json.
    /// Every value has a safe default, so the agent works even with an empty section.
    /// </summary>
    public sealed class AgentOptions
    {
        /// <summary>Sets the default values. appsettings.json can override any of them.</summary>
        public AgentOptions()
        {
            AppName = "Shop Assistant";
            FallbackAnswer = "Sorry, I can't help with that.";
            MaxSteps = 6;
            MaxTokensPerMessage = 20000;
            MaxHistoryMessages = 20;
            MaxInputChars = 2000;
            Temperature = null;
            MaxToolCallsPerRun = 10;
            ToolTimeoutSeconds = 10;
            ShrinkOldToolResults = true;
            Pricing = new AgentPricing();
            Evals = new AgentEvalOptions();
        }

        /// <summary>The assistant's name. Used in the system prompt ({{APP_NAME}}) and in telemetry.</summary>
        public string AppName { get; set; }

        /// <summary>
        /// Path to the SYSTEM PROMPT file (the agent's job description), e.g. "Agent/system-prompt.md".
        /// A relative path is read from the app's content root (the API project folder).
        /// Keeping the prompt in a file means you can change it without recompiling.
        /// </summary>
        public string SystemPromptPath { get; set; }

        /// <summary>The system prompt written inline. Used only when SystemPromptPath is empty.</summary>
        public string SystemPrompt { get; set; }

        /// <summary>The polite answer shown when a guardrail blocks a message.</summary>
        public string FallbackAnswer { get; set; }

        /// <summary>
        /// Max think-act rounds per user message. Stops a confused model from calling tools
        /// forever (and burning money).
        /// </summary>
        public int MaxSteps { get; set; }

        /// <summary>Token budget for one user message, across all its LLM calls. Another cost safety net.</summary>
        public int MaxTokensPerMessage { get; set; }

        /// <summary>How many recent messages to keep. Old ones are dropped so prompts stay small and cheap.</summary>
        public int MaxHistoryMessages { get; set; }

        /// <summary>Longest user message we accept, in characters.</summary>
        public int MaxInputChars { get; set; }

        /// <summary>
        /// Max tool calls in one answer, across all steps. MaxSteps limits the ROUNDS, but one round can
        /// ask for many tools at once, so this is a second cap on work (and database load).
        /// </summary>
        public int MaxToolCallsPerRun { get; set; }

        /// <summary>How long one tool may run before it is cancelled, in seconds.</summary>
        public int ToolTimeoutSeconds { get; set; }

        /// <summary>
        /// True (the default) = when an answer is finished, its tool results are replaced in the saved
        /// conversation by short stubs that keep the ids, e.g. "[search_products: 10 items, ids 12, 45]".
        /// The answer the user saw already holds the facts, so later questions don't pay for the full
        /// results again. False = keep everything (only useful to measure the difference).
        /// </summary>
        public bool ShrinkOldToolResults { get; set; }

        /// <summary>
        /// 0 = predictable, 1 = creative. Null (the default) means "don't send it at all",
        /// because gpt-5 reasoning models reject the temperature setting.
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// A secret marker hidden in the system prompt (see SecretLeakGuardrail).
        /// Leave it empty and a random one is made at startup, so every environment has its own.
        /// </summary>
        public string Canary { get; set; }

        /// <summary>Token prices, used to show an ESTIMATED cost for each run.</summary>
        public AgentPricing Pricing { get; set; }

        /// <summary>Settings for the eval runner.</summary>
        public AgentEvalOptions Evals { get; set; }
    }

    /// <summary>
    /// Prices per million tokens, in US dollars. Copy them from the provider's price list.
    /// The cost we show is always an ESTIMATE: the real bill comes from the provider.
    /// </summary>
    public sealed class AgentPricing
    {
        /// <summary>Price of one million INPUT tokens (what we send to the model).</summary>
        public decimal InputPerMillionUsd { get; set; }

        /// <summary>Price of one million OUTPUT tokens (what the model writes back).</summary>
        public decimal OutputPerMillionUsd { get; set; }

        /// <summary>Works out the estimated cost of a number of tokens, rounded to 6 decimal places.</summary>
        public decimal EstimateCost(int inputTokens, int outputTokens)
        {
            decimal input = inputTokens / 1000000m * InputPerMillionUsd;
            decimal output = outputTokens / 1000000m * OutputPerMillionUsd;
            return Math.Round(input + output, 6);
        }
    }

    /// <summary>Settings for evals (automatic tests of the agent's behaviour).</summary>
    public sealed class AgentEvalOptions
    {
        /// <summary>Sets the default values.</summary>
        public AgentEvalOptions()
        {
            CasesPath = "Agent/evals/cases.json";
            BaselinePath = "Agent/evals/baseline.json";
            DefaultRepeat = 3;
            JudgePassScore = 4;
            MaxDropPoints = 5;
        }

        /// <summary>Where the eval cases live (relative to the content root).</summary>
        public string CasesPath { get; set; }

        /// <summary>Where the saved baseline lives (relative to the content root).</summary>
        public string BaselinePath { get; set; }

        /// <summary>
        /// How many times each case runs. LLM answers change from run to run, so one pass proves little.
        /// </summary>
        public int DefaultRepeat { get; set; }

        /// <summary>The LLM judge's score (1-5) needed to pass.</summary>
        public int JudgePassScore { get; set; }

        /// <summary>The regression gate fails when the pass rate drops more than this many points below the baseline.</summary>
        public double MaxDropPoints { get; set; }
    }
}
