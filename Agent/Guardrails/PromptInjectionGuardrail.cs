using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Agent.Guardrails
{
    /// <summary>
    /// "Prompt injection" = a user (or a web page / document the agent reads) tries to
    /// override the agent's instructions: "Ignore all previous instructions and...".
    ///
    /// This simple version looks for well-known attack phrases. It will NOT catch everything:
    /// real systems add a classifier service (e.g. Azure AI Content Safety "Prompt Shields").
    /// Defence in depth: this check + a strong system prompt + read-only tools that validate their inputs.
    /// </summary>
    public sealed class PromptInjectionGuardrail : IGuardrail
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        private static readonly List<Regex> Patterns = new List<Regex>
        {
            new Regex(@"\b(ignore|disregard|forget)\b.{0,30}\b(previous|prior|above|earlier|your)\b.{0,20}\b(instructions?|rules|prompt)", Options),
            new Regex(@"\b(reveal|show|print|repeat|tell me)\b.{0,30}\b(system prompt|hidden instructions|your instructions)", Options),
            new Regex(@"\byou are now\b.{0,40}\b(unrestricted|jailbroken|DAN|without rules)", Options),
            new Regex(@"\b(developer|god|admin) mode\b", Options),
        };

        /// <summary>Short name shown in logs.</summary>
        public string Name
        {
            get { return "prompt-injection"; }
        }

        /// <summary>Blocks the text if any known attack phrase appears in it.</summary>
        public GuardrailResult Check(string text)
        {
            foreach (Regex pattern in Patterns)
            {
                if (pattern.IsMatch(text))
                    return GuardrailResult.Block(text, "The message looks like an attempt to override the assistant's instructions.");
            }

            return GuardrailResult.Pass(text);
        }
    }
}
