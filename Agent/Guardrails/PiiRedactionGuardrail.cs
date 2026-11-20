using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Agent.Guardrails
{
    /// <summary>
    /// PII = Personally Identifiable Information (emails, phone numbers, SSNs, card numbers).
    ///
    /// This guardrail does not block. It REPLACES the sensitive part with a placeholder, so:
    ///  - on INPUT: private data is never sent to the external LLM provider, and never logged;
    ///  - on OUTPUT: the model can't leak private data back to the user.
    /// The runner also uses it to mask what it saves in run records.
    /// </summary>
    public sealed class PiiRedactionGuardrail : IGuardrail
    {
        /// <summary>One "find this pattern, replace it with that placeholder" rule.</summary>
        private sealed class RedactionRule
        {
            public RedactionRule(string pattern, string replacement)
            {
                Pattern = new Regex(pattern, RegexOptions.Compiled);
                Replacement = replacement;
            }

            public Regex Pattern { get; }
            public string Replacement { get; }
        }

        // Order matters: card numbers before phone numbers, because a card number contains digit groups too.
        private static readonly List<RedactionRule> Rules = new List<RedactionRule>
        {
            new RedactionRule(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", "[REDACTED_EMAIL]"),
            new RedactionRule(@"\b\d{3}-\d{2}-\d{4}\b", "[REDACTED_SSN]"),
            new RedactionRule(@"\b(?:\d[ -]?){13,16}\b", "[REDACTED_CARD]"),
            new RedactionRule(@"\(?\b\d{3}\)?[ .-]?\d{3}[ .-]?\d{4}\b", "[REDACTED_PHONE]"),
        };

        /// <summary>Short name shown in logs.</summary>
        public string Name
        {
            get { return "pii-redaction"; }
        }

        /// <summary>Replaces every piece of PII it finds with a placeholder. Never blocks.</summary>
        public GuardrailResult Check(string text)
        {
            return GuardrailResult.Pass(Redact(text));
        }

        /// <summary>Returns the text with every piece of PII replaced by a placeholder.</summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string cleaned = text;
            foreach (RedactionRule rule in Rules)
                cleaned = rule.Pattern.Replace(cleaned, rule.Replacement);

            return cleaned;
        }
    }
}
