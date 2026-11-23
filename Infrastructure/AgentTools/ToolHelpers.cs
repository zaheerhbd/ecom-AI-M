using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// Small helpers shared by the shop's agent tools.
    /// </summary>
    public static class ToolHelpers
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            // camelCase ("inStock") is what the model sees in most JSON, so it reads it best.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // No indentation: every space we send costs input tokens.
            WriteIndented = false
        };

        /// <summary>Turns a result object into compact JSON for the model to read.</summary>
        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }

        /// <summary>
        /// Makes names comparable: lower case, no spaces, dashes or dots.
        /// So "Polar Path", "polarpath" and "polar-path" all match the same brand.
        /// </summary>
        public static string NormaliseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            var builder = new StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Finds a name in a list, ignoring case and spaces. Also accepts a simple plural/singular
        /// difference ("hat" finds "Hats"). Returns null when nothing matches.
        /// </summary>
        public static string FindName(IEnumerable<string> knownNames, string wanted)
        {
            string target = NormaliseName(wanted);
            if (target.Length == 0)
                return null;

            foreach (string name in knownNames)
            {
                string candidate = NormaliseName(name);
                if (candidate == target || candidate == target + "s" || candidate + "s" == target)
                    return name;
            }
            return null;
        }

        /// <summary>Cuts long text (e.g. a description) so tool results stay small and cheap.</summary>
        public static string Shorten(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength).TrimEnd() + "...";
        }

        /// <summary>"a, b, c" for error messages that list the valid choices.</summary>
        public static string JoinNames(IEnumerable<string> names)
        {
            return string.Join(", ", names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        }
    }
}
