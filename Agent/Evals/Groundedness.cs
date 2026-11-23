using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Agent.Evals
{
    /// <summary>The result of a groundedness check: facts backed up by data, and facts that were not.</summary>
    public sealed class GroundednessResult
    {
        /// <summary>Creates a result.</summary>
        public GroundednessResult(int groundedCount, List<string> madeUp)
        {
            GroundedCount = groundedCount;
            MadeUp = madeUp;
        }

        /// <summary>How many prices / stock numbers in the answer appear in the evidence.</summary>
        public int GroundedCount { get; }

        /// <summary>The prices / stock numbers that appear NOWHERE in the evidence, e.g. "$19.99".</summary>
        public List<string> MadeUp { get; }

        /// <summary>All facts that were checked.</summary>
        public int CheckedCount
        {
            get { return GroundedCount + MadeUp.Count; }
        }
    }

    /// <summary>
    /// GROUNDEDNESS: "is every number in the answer backed up by real data?"
    ///
    /// An LLM can write a price that sounds right but isn't. That is the worst mistake a shop
    /// assistant can make, so we check it with plain code (no LLM call, free, always the same verdict):
    ///
    ///   1. Pull every PRICE ("$12.99", "12.99 USD") and STOCK NUMBER ("3 left", "in stock: 12")
    ///      out of the answer with regular expressions.
    ///   2. Pull every number out of the EVIDENCE: this answer's tool results, plus the earlier
    ///      conversation (the user's own words, and earlier answers that were checked when they were made).
    ///   3. A fact whose number is in the evidence is "grounded"; anything else is "made up".
    ///
    /// Numbers are compared as numbers, so "$12" matches "12.00" in a tool result.
    /// It is a heuristic: it can't tell WHICH product a price belongs to, only that the number exists.
    /// </summary>
    public static class Groundedness
    {
        private const string Number = @"\d[\d,]*(?:\.\d+)?";

        // "$12.99", "$ 1,200"
        private static readonly Regex DollarPrice = new Regex(@"\$\s?(" + Number + ")", RegexOptions.Compiled);

        // "12.99 USD", "40 dollars"
        private static readonly Regex WordPrice = new Regex("(" + Number + @")\s?(?:USD|US dollars|dollars)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "3 left", "12 in stock", "5 units"
        private static readonly Regex StockAfter = new Regex(@"\b(\d+)\s+(?:left|in stock|units?|pieces?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "in stock: 12", "stock quantity 4"
        private static readonly Regex StockBefore = new Regex(@"\b(?:in stock|stock(?: level| quantity)?)\s*[:=]?\s*(\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AnyNumber = new Regex(Number, RegexOptions.Compiled);

        /// <summary>Checks the answer's prices and stock numbers against the evidence texts.</summary>
        public static GroundednessResult Check(string answer, IEnumerable<string> evidence)
        {
            HashSet<decimal> known = new HashSet<decimal>();
            foreach (string text in evidence)
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                foreach (Match match in AnyNumber.Matches(text))
                {
                    decimal value;
                    if (TryParse(match.Value, out value))
                        known.Add(value);
                }
            }

            int grounded = 0;
            List<string> madeUp = new List<string>();
            foreach (KeyValuePair<string, decimal> fact in ExtractFacts(answer ?? ""))
            {
                if (known.Contains(fact.Value))
                    grounded++;
                else
                    madeUp.Add(fact.Key);
            }

            return new GroundednessResult(grounded, madeUp);
        }

        /// <summary>The prices and stock numbers in a text, as (text as written, value). Each written fact once.</summary>
        internal static List<KeyValuePair<string, decimal>> ExtractFacts(string text)
        {
            List<KeyValuePair<string, decimal>> facts = new List<KeyValuePair<string, decimal>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Regex pattern in new[] { DollarPrice, WordPrice, StockAfter, StockBefore })
            {
                foreach (Match match in pattern.Matches(text))
                {
                    decimal value;
                    string written = match.Value.Trim();
                    if (TryParse(match.Groups[1].Value, out value) && seen.Add(written))
                        facts.Add(new KeyValuePair<string, decimal>(written, value));
                }
            }

            return facts;
        }

        /// <summary>"1,200.50" -> 1200.5. Trailing zeros don't matter for decimal equality (12 == 12.00).</summary>
        private static bool TryParse(string text, out decimal value)
        {
            return decimal.TryParse(text.Replace(",", "").TrimEnd('.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
