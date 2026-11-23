namespace Agent.Guardrails
{
    /// <summary>
    /// The result of one guardrail check.
    ///  - Allowed = false  -> stop here, the text is blocked.
    ///  - Allowed = true   -> continue, using Text (a guardrail may have CHANGED it, e.g. hidden an email).
    /// </summary>
    public sealed class GuardrailResult
    {
        /// <summary>Creates a result. Use the Pass and Block helpers below instead of calling this directly.</summary>
        public GuardrailResult(bool allowed, string text, string reason)
        {
            Allowed = allowed;
            Text = text;
            Reason = reason;
        }

        /// <summary>False means the text must not go any further.</summary>
        public bool Allowed { get; }

        /// <summary>The text after the check (it may have been cleaned).</summary>
        public string Text { get; }

        /// <summary>Why it was blocked. Null when allowed.</summary>
        public string Reason { get; }

        /// <summary>The text is fine (possibly cleaned): carry on.</summary>
        public static GuardrailResult Pass(string text)
        {
            return new GuardrailResult(true, text, null);
        }

        /// <summary>The text is not allowed: stop, and say why.</summary>
        public static GuardrailResult Block(string text, string reason)
        {
            return new GuardrailResult(false, text, reason);
        }
    }

    /// <summary>
    /// A GUARDRAIL is a safety check that runs BEFORE text reaches the model (input guardrail)
    /// or BEFORE the model's answer reaches the user (output guardrail).
    ///
    /// Important idea: guardrails are plain code, not the model. A model can be tricked;
    /// a regular expression can't be talked out of its job. Use both layers:
    /// instructions in the system prompt AND code checks like these.
    /// </summary>
    public interface IGuardrail
    {
        /// <summary>Short name shown in logs and in the agent's step list.</summary>
        string Name { get; }

        /// <summary>Checks the text and returns Pass (maybe with cleaned text) or Block.</summary>
        GuardrailResult Check(string text);
    }
}
