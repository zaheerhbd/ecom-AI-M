using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Agent.Evals
{
    /// <summary>Reads eval cases from a JSON file and checks they make sense.</summary>
    public static class EvalCaseLoader
    {
        /// <summary>The JSON settings used for case files: names ignore case, and comments and trailing commas are allowed.</summary>
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Loads the cases from a file. Throws with a clear message if the file is missing or wrong.</summary>
        public static List<EvalCase> LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("The eval cases file was not found.", fullPath);

            return Parse(File.ReadAllText(fullPath));
        }

        /// <summary>Reads cases from JSON text (a list of case objects).</summary>
        public static List<EvalCase> Parse(string json)
        {
            List<EvalCase> cases = JsonSerializer.Deserialize<List<EvalCase>>(json, JsonOptions) ?? new List<EvalCase>();
            Validate(cases);
            return cases;
        }

        /// <summary>Every case needs a unique id and an input; anything else is a mistake in the file.</summary>
        private static void Validate(List<EvalCase> cases)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (EvalCase evalCase in cases)
            {
                if (string.IsNullOrWhiteSpace(evalCase.Id))
                    throw new InvalidDataException("An eval case has no id.");

                if (!ids.Add(evalCase.Id))
                    throw new InvalidDataException("Two eval cases have the id '" + evalCase.Id + "'.");

                if (string.IsNullOrWhiteSpace(evalCase.Input))
                    throw new InvalidDataException("Eval case '" + evalCase.Id + "' has no input.");

                // JSON "null" for a list becomes null in C#; treat it as an empty list.
                if (evalCase.MustContain == null)
                    evalCase.MustContain = new List<string>();
                if (evalCase.MustNotContain == null)
                    evalCase.MustNotContain = new List<string>();

                if (evalCase.ExpectedTools != null)
                {
                    foreach (ExpectedToolCall expected in evalCase.ExpectedTools)
                    {
                        if (string.IsNullOrWhiteSpace(expected.Name))
                            throw new InvalidDataException("Eval case '" + evalCase.Id + "' has an expected tool with no name.");

                        if (expected.Args == null)
                            expected.Args = new Dictionary<string, JsonElement>();
                    }
                }
            }
        }
    }
}
