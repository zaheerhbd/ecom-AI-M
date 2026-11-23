namespace Agent.Guardrails
{
    /// <summary>What one guardrail did with the text.</summary>
    public enum GuardrailOutcome
    {
        /// <summary>The text was fine and was not changed (green chip in the UI).</summary>
        Passed,

        /// <summary>The text was allowed but cleaned, e.g. an email was masked (amber chip).</summary>
        Modified,

        /// <summary>The text was stopped (red chip).</summary>
        Blocked,

        /// <summary>The guardrail never ran, because an earlier step already stopped the request (grey chip).</summary>
        Skipped
    }

    /// <summary>The two places guardrails run.</summary>
    public static class GuardrailStages
    {
        /// <summary>On the user's message, before the model sees it.</summary>
        public const string Input = "input";

        /// <summary>On the model's answer, before the user sees it.</summary>
        public const string Output = "output";
    }

    /// <summary>
    /// A record of one guardrail check: which stage, which guardrail, what happened, why and how long it took.
    /// The run record keeps a list of these so the UI can show one coloured chip per check.
    /// </summary>
    public sealed class GuardrailCheck
    {
        /// <summary>Empty constructor, needed when a saved run is read back from JSON.</summary>
        public GuardrailCheck()
        {
        }

        /// <summary>Creates a check record.</summary>
        public GuardrailCheck(string stage, string name, GuardrailOutcome outcome, string reason, double durationMs)
        {
            Stage = stage;
            Name = name;
            Outcome = outcome;
            Reason = reason;
            DurationMs = durationMs;
        }

        /// <summary>"input" or "output" (see GuardrailStages).</summary>
        public string Stage { get; set; }

        /// <summary>The guardrail's name, e.g. "prompt-injection".</summary>
        public string Name { get; set; }

        /// <summary>Passed, Modified, Blocked or Skipped.</summary>
        public GuardrailOutcome Outcome { get; set; }

        /// <summary>Why it blocked or was skipped. Null when it passed.</summary>
        public string Reason { get; set; }

        /// <summary>How long the check took, in milliseconds.</summary>
        public double DurationMs { get; set; }
    }
}
