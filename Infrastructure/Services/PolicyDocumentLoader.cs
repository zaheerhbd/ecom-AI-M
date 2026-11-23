using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Core.Models;

namespace Infrastructure.Services
{
    /// <summary>
    /// Reads the shop's policy files (Data/SeedData/policies/*.md) and turns them into documents
    /// the AI can search: ONE document per "## " section, so each answer can point at the exact
    /// part of a policy (for example "Returns and refunds policy – Return window").
    ///
    /// Each file starts with a small front-matter block that holds its title:
    /// <code>
    /// ---
    /// title: Returns and refunds policy
    /// ---
    /// </code>
    /// The files are copied next to the app when it is built (see Infrastructure.csproj).
    /// </summary>
    public static class PolicyDocumentLoader
    {
        public static string DefaultFolder =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Data", "SeedData", "policies");

        /// <summary>Loads every policy section from the folder. Returns an empty list if the folder is missing.</summary>
        public static IReadOnlyList<AiDocument> Load(string folder = null)
        {
            folder ??= DefaultFolder;
            if (!Directory.Exists(folder))
                return Array.Empty<AiDocument>();

            return Directory.GetFiles(folder, "*.md")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase) // same order on every machine
                .SelectMany(f => ParseFile(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)))
                .ToList();
        }

        /// <summary>Splits one policy file into one document per "## " section.</summary>
        public static IEnumerable<AiDocument> ParseFile(string slug, string markdown)
        {
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var index = 0;
            var title = slug;

            // Front matter: "---", "title: ...", "---"
            if (lines.Length > 0 && lines[0].Trim() == "---")
            {
                index = 1;
                for (; index < lines.Length && lines[index].Trim() != "---"; index++)
                {
                    var line = lines[index].Trim();
                    if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                        title = line.Substring("title:".Length).Trim();
                }
                index++; // skip the closing "---"
            }

            string heading = null;
            var body = new StringBuilder();
            var sectionNumber = 0;
            var documents = new List<AiDocument>();

            void Flush()
            {
                var text = body.ToString().Trim();
                if (heading != null && text.Length > 0)
                {
                    sectionNumber++;
                    documents.Add(new AiDocument
                    {
                        Id = $"policy-{slug}-{sectionNumber}",
                        SourceType = "policy",
                        Title = $"{title} – {heading}",
                        Text = $"{title}. {heading}. {text}",
                        Metadata = new Dictionary<string, string>
                        {
                            ["policy"] = slug,
                            ["section"] = heading
                        }
                    });
                }
                body.Clear();
            }

            for (; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith("## "))
                {
                    Flush();
                    heading = line.Substring(3).Trim();
                }
                else
                {
                    body.AppendLine(line);
                }
            }
            Flush();

            return documents;
        }
    }
}
