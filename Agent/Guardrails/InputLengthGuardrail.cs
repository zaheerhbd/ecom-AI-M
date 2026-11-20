namespace Agent.Guardrails
{
    /// <summary>
    /// Blocks empty or very long messages. Long inputs cost money (more tokens)
    /// and are a common way to hide attacks, so this is the cheapest guardrail you can add.
    /// </summary>
    public sealed class InputLengthGuardrail : IGuardrail
    {
        private readonly int _maxChars;

        /// <summary>Creates the guardrail with the longest message allowed, in characters.</summary>
        public InputLengthGuardrail(int maxChars)
        {
            _maxChars = maxChars;
        }

        /// <summary>Short name shown in logs.</summary>
        public string Name
        {
            get { return "input-length"; }
        }

        /// <summary>Blocks empty text and text longer than the limit.</summary>
        public GuardrailResult Check(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return GuardrailResult.Block(text, "The message is empty.");

            if (text.Length > _maxChars)
                return GuardrailResult.Block(text, "The message is too long (" + text.Length + " characters, limit is " + _maxChars + ").");

            return GuardrailResult.Pass(text);
        }
    }
}
