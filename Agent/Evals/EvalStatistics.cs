using System;
using System.Collections.Generic;

namespace Agent.Evals
{
    /// <summary>
    /// Small maths helpers for the eval summary.
    ///  - MEDIAN: the middle value. Better than the average for latency, because one very slow call
    ///    can drag the average up a lot.
    ///  - P95 (95th percentile): 95% of values are at or below this. It shows how slow the
    ///    "bad but not rare" cases are, which is what users notice.
    /// </summary>
    public static class EvalStatistics
    {
        /// <summary>The middle value (0 for an empty list).</summary>
        public static double Median(IEnumerable<double> values)
        {
            List<double> sorted = Sorted(values);
            if (sorted.Count == 0)
                return 0;

            int middle = sorted.Count / 2;
            if (sorted.Count % 2 == 1)
                return sorted[middle];

            return (sorted[middle - 1] + sorted[middle]) / 2;
        }

        /// <summary>The value at the given percentile, using the "nearest rank" method (0 for an empty list).</summary>
        public static double Percentile(IEnumerable<double> values, double percentile)
        {
            List<double> sorted = Sorted(values);
            if (sorted.Count == 0)
                return 0;

            // Nearest rank: the smallest value with at least `percentile` % of values at or below it.
            int rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count);
            int index = Math.Min(Math.Max(rank, 1), sorted.Count) - 1;
            return sorted[index];
        }

        /// <summary>A sorted copy of the values.</summary>
        private static List<double> Sorted(IEnumerable<double> values)
        {
            List<double> sorted = new List<double>(values);
            sorted.Sort();
            return sorted;
        }
    }
}
