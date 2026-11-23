using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Runs
{
    /// <summary>
    /// Keeps runs in a list in memory. Used by the tests, and as the default until the shop
    /// plugs in its database store. Lost on restart.
    /// Only the newest 1,000 runs are kept, so memory can't grow forever.
    /// </summary>
    public sealed class InMemoryAgentRunStore : IAgentRunStore
    {
        private const int MaxRuns = 1000;
        private const int TopToolCount = 5;

        private readonly object _lock = new object();
        private readonly List<AgentRunRecord> _runs = new List<AgentRunRecord>(); // oldest first

        /// <summary>Saves one finished run, dropping the oldest when the list is full.</summary>
        public Task SaveAsync(AgentRunRecord run, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _runs.Add(run);
                if (_runs.Count > MaxRuns)
                    _runs.RemoveAt(0);
            }

            return Task.CompletedTask;
        }

        /// <summary>Returns one run, or null.</summary>
        public Task<AgentRunRecord> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                foreach (AgentRunRecord run in _runs)
                {
                    if (run.Id == id)
                        return Task.FromResult(run);
                }
            }

            return Task.FromResult<AgentRunRecord>(null);
        }

        /// <summary>Returns one page of runs, newest first.</summary>
        public Task<AgentRunPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            // Keep the paging numbers sensible whatever the caller sends.
            int safePage = Math.Max(1, page);
            int safeSize = Math.Min(Math.Max(1, pageSize), 100);

            List<AgentRunRecord> items = new List<AgentRunRecord>();
            int total;

            lock (_lock)
            {
                total = _runs.Count;

                // The list is oldest first, so walk it backwards to get newest first.
                int skip = (safePage - 1) * safeSize;
                for (int i = _runs.Count - 1 - skip; i >= 0 && items.Count < safeSize; i--)
                    items.Add(_runs[i]);
            }

            return Task.FromResult(new AgentRunPage(items, total, safePage, safeSize));
        }

        /// <summary>Adds up totals across all runs.</summary>
        public Task<AgentRunStats> GetStatsAsync(CancellationToken cancellationToken)
        {
            AgentRunStats stats = new AgentRunStats();
            Dictionary<string, int> toolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            double totalMs = 0;

            lock (_lock)
            {
                foreach (AgentRunRecord run in _runs)
                {
                    stats.TotalRuns++;
                    if (run.Blocked)
                        stats.BlockedRuns++;
                    if (run.Error != null)
                        stats.ErrorRuns++;

                    totalMs += run.DurationMs;
                    stats.TotalInputTokens += run.InputTokens;
                    stats.TotalOutputTokens += run.OutputTokens;
                    stats.TotalEstimatedCostUsd += run.EstimatedCostUsd;

                    foreach (string tool in run.ToolsUsed)
                    {
                        int count;
                        toolCounts.TryGetValue(tool, out count);
                        toolCounts[tool] = count + 1;
                    }
                }
            }

            if (stats.TotalRuns > 0)
            {
                stats.AverageDurationMs = Math.Round(totalMs / stats.TotalRuns, 1);
                stats.BlockRate = Math.Round((double)stats.BlockedRuns / stats.TotalRuns, 3);
            }

            stats.TopTools = PickTopTools(toolCounts);
            return Task.FromResult(stats);
        }

        /// <summary>Sorts the tool counts, most used first, and keeps the top few.</summary>
        private static List<ToolUsageCount> PickTopTools(Dictionary<string, int> toolCounts)
        {
            List<ToolUsageCount> all = new List<ToolUsageCount>();
            foreach (KeyValuePair<string, int> entry in toolCounts)
                all.Add(new ToolUsageCount(entry.Key, entry.Value));

            all.Sort((left, right) => right.Count.CompareTo(left.Count));

            if (all.Count > TopToolCount)
                all.RemoveRange(TopToolCount, all.Count - TopToolCount);

            return all;
        }
    }
}
