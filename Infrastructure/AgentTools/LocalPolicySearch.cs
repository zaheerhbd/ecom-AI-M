using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Infrastructure.Services;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// A small KEYWORD search over the policy files, used when Azure AI Search is not switched on
    /// (always on the laptop: see decision D7, no dev index).
    ///
    /// How it scores a policy section:
    ///  1. Split the question into words, drop filler words ("the", "can", "I"...).
    ///  2. Reduce each word to a simple stem ("returns" -> "return", "shipping" -> "ship").
    ///  3. Each question word found in the section adds 1 point; found in the title adds 2 more.
    ///  4. The score is divided by the number of question words, so it is between 0 and about 3.
    /// It is much simpler than a vector search, but the policy files are short and use plain words.
    /// </summary>
    public static class LocalPolicySearch
    {
        // Loaded once, the first time it is needed. The files don't change while the app runs.
        private static readonly Lazy<List<IndexedSection>> Sections = new Lazy<List<IndexedSection>>(BuildIndex);

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "of", "to", "in", "on", "for", "with", "at", "by", "from", "is", "are",
            "be", "it", "its", "i", "you", "your", "we", "our", "me", "my", "can", "could", "do", "does", "did",
            "what", "whats", "how", "when", "where", "which", "who", "why", "if", "any", "there", "this", "that",
            "have", "has", "get", "will", "would", "should", "please", "tell", "about", "us", "much", "long"
        };

        /// <summary>The best matching sections, best first. Sections with no matching word are left out.</summary>
        public static IReadOnlyList<PolicyMatch> Search(string question, int maxResults)
        {
            List<string> words = Tokenise(question).Distinct().ToList();
            if (words.Count == 0)
                return Array.Empty<PolicyMatch>();

            var matches = new List<PolicyMatch>();
            foreach (IndexedSection section in Sections.Value)
            {
                double points = 0;
                foreach (string word in words)
                {
                    if (section.BodyWords.Contains(word)) points += 1;
                    if (section.TitleWords.Contains(word)) points += 2;
                }

                if (points > 0)
                    matches.Add(new PolicyMatch(section.Document, Math.Round(points / words.Count, 3)));
            }

            return matches.OrderByDescending(m => m.Score).Take(maxResults).ToList();
        }

        /// <summary>Lower-case words, stemmed, without filler words.</summary>
        private static IEnumerable<string> Tokenise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            string[] parts = text.ToLowerInvariant()
                .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '*', '/', '\'', '–', '-' },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (part.Length < 2 || StopWords.Contains(part))
                    continue;
                yield return Stem(part);
            }
        }

        /// <summary>A very small stemmer: enough to match "returns"/"returned"/"returning" with "return".</summary>
        private static string Stem(string word)
        {
            if (word.Length > 5 && word.EndsWith("ing")) return TrimDouble(word.Substring(0, word.Length - 3));
            if (word.Length > 4 && word.EndsWith("ed")) return TrimDouble(word.Substring(0, word.Length - 2));
            if (word.Length > 4 && word.EndsWith("ies")) return word.Substring(0, word.Length - 3) + "y";
            if (word.Length > 3 && word.EndsWith("s") && !word.EndsWith("ss")) return word.Substring(0, word.Length - 1);
            return word;
        }

        /// <summary>"shipp" -> "ship", so "shipping" and "ship" meet.</summary>
        private static string TrimDouble(string word)
        {
            int n = word.Length;
            if (n > 2 && word[n - 1] == word[n - 2] && word[n - 1] != 'l' && word[n - 1] != 's')
                return word.Substring(0, n - 1);
            return word;
        }

        private static List<IndexedSection> BuildIndex()
        {
            return PolicyDocumentLoader.Load()
                .Select(d => new IndexedSection
                {
                    Document = d,
                    TitleWords = new HashSet<string>(Tokenise(d.Title)),
                    BodyWords = new HashSet<string>(Tokenise(d.Text))
                })
                .ToList();
        }

        private sealed class IndexedSection
        {
            public AiDocument Document;
            public HashSet<string> TitleWords;
            public HashSet<string> BodyWords;
        }
    }

    /// <summary>One policy section and how well it matched.</summary>
    public sealed class PolicyMatch
    {
        public PolicyMatch(AiDocument document, double score)
        {
            Document = document;
            Score = score;
        }

        public AiDocument Document { get; }
        public double Score { get; }
    }
}
