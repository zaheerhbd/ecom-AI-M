using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Agent.Evals
{
    /// <summary>The saved result of one case in a baseline.</summary>
    public sealed class EvalBaselineCase
    {
        /// <summary>The case id.</summary>
        public string Id { get; set; }

        /// <summary>How many repeats passed.</summary>
        public int PassCount { get; set; }

        /// <summary>How many repeats ran.</summary>
        public int RepeatCount { get; set; }
    }

    /// <summary>
    /// A BASELINE is a saved eval result you have accepted as "good". Later runs are compared with
    /// it: if the pass rate drops by more than a few points, something got worse (a "regression"),
    /// and the gate fails before the change is merged.
    /// </summary>
    public sealed class EvalBaseline
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>Starts with no cases.</summary>
        public EvalBaseline()
        {
            Cases = new List<EvalBaselineCase>();
        }

        /// <summary>When the baseline was saved, in UTC.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The model that produced it.</summary>
        public string Model { get; set; }

        /// <summary>How many times each case ran.</summary>
        public int Repeat { get; set; }

        /// <summary>The attempt pass rate, from 0 to 1.</summary>
        public double PassRate { get; set; }

        /// <summary>One entry per case.</summary>
        public List<EvalBaselineCase> Cases { get; set; }

        /// <summary>Builds a baseline from a finished run and its case results.</summary>
        public static EvalBaseline FromRun(EvalRunRecord run, List<EvalCaseResultRecord> results)
        {
            EvalBaseline baseline = new EvalBaseline();
            baseline.CreatedAt = DateTime.UtcNow;
            baseline.Model = run.Model;
            baseline.Repeat = run.Repeat;
            baseline.PassRate = run.PassRate;

            foreach (EvalCaseResultRecord result in results)
            {
                EvalBaselineCase item = new EvalBaselineCase();
                item.Id = result.CaseId;
                item.PassCount = result.PassCount;
                item.RepeatCount = result.RepeatCount;
                baseline.Cases.Add(item);
            }

            return baseline;
        }

        /// <summary>
        /// True when the new pass rate is more than maxDropPoints percentage points below the baseline.
        /// Example: baseline 0.90, new 0.84, max drop 5 -> a 6-point drop -> regression.
        /// </summary>
        public static bool IsRegression(double baselinePassRate, double newPassRate, double maxDropPoints)
        {
            double dropPoints = (baselinePassRate - newPassRate) * 100.0;

            // A tiny allowance so rounding (e.g. 4.9999999) never fails the gate by accident.
            return dropPoints > maxDropPoints + 0.0001;
        }

        /// <summary>Reads a baseline file.</summary>
        public static EvalBaseline Load(string fullPath)
        {
            string json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<EvalBaseline>(json, EvalCaseLoader.JsonOptions) ?? new EvalBaseline();
        }

        /// <summary>Writes the baseline to a file, creating the folder if needed.</summary>
        public void Save(string fullPath)
        {
            string folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(fullPath, JsonSerializer.Serialize(this, WriteOptions));
        }

        /// <summary>The ids of cases that passed fewer repeats now than in the baseline (as a share of their repeats).</summary>
        public List<string> FindWorseCases(List<EvalCaseResultRecord> results)
        {
            Dictionary<string, EvalBaselineCase> saved = new Dictionary<string, EvalBaselineCase>(StringComparer.OrdinalIgnoreCase);
            foreach (EvalBaselineCase item in Cases)
                saved[item.Id] = item;

            List<string> worse = new List<string>();
            foreach (EvalCaseResultRecord result in results)
            {
                EvalBaselineCase before;
                if (!saved.TryGetValue(result.CaseId, out before) || before.RepeatCount == 0 || result.RepeatCount == 0)
                    continue;

                double rateBefore = (double)before.PassCount / before.RepeatCount;
                double rateNow = (double)result.PassCount / result.RepeatCount;
                if (rateNow < rateBefore)
                    worse.Add(result.CaseId + " (" + before.PassCount + "/" + before.RepeatCount + " -> " + result.PassCount + "/" + result.RepeatCount + ")");
            }

            return worse;
        }
    }
}
