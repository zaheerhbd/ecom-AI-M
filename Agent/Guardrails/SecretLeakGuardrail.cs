using System;

namespace Agent.Guardrails
{
    /// <summary>
    /// OUTPUT guardrail using a "canary": we hide a secret marker in the system prompt.
    /// The model is told never to repeat it. If the marker ever shows up in an answer,
    /// the model was tricked into leaking its instructions, so we block the answer.
    /// It's like a dye pack in a bag of bank money.
    /// </summary>
    public sealed class SecretLeakGuardrail : IGuardrail
    {
        private readonly string _canary;

        /// <summary>Creates the guardrail with the secret marker to watch for.</summary>
        public SecretLeakGuardrail(string canary)
        {
            _canary = canary;
        }

        /// <summary>Short name shown in logs.</summary>
        public string Name
        {
            get { return "secret-leak"; }
        }

        /// <summary>Blocks the answer if it contains the secret marker.</summary>
        public GuardrailResult Check(string text)
        {
            // Without a canary there is nothing to look for.
            if (string.IsNullOrEmpty(_canary))
                return GuardrailResult.Pass(text);

            if (text.Contains(_canary, StringComparison.OrdinalIgnoreCase))
                return GuardrailResult.Block(text, "The answer contained internal instructions and was blocked.");

            return GuardrailResult.Pass(text);
        }
    }
}
