using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Runs;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    /// <summary>
    /// Saves agent runs in the AgentRuns table (instead of the engine's in-memory list), so they
    /// survive restarts and the runs page can show them. Old runs are deleted after 30 days by
    /// AgentRunCleanupService (decision D3).
    /// </summary>
    public class EfAgentRunStore : IAgentRunStore
    {
        private const int MaxPageSize = 100;
        private const int TopToolCount = 5;

        private readonly StoreContext _db;

        public EfAgentRunStore(StoreContext db)
        {
            _db = db;
        }

        public async Task SaveAsync(AgentRunRecord run, CancellationToken cancellationToken)
        {
            _db.AgentRuns.Add(run);
            await _db.SaveChangesAsync(cancellationToken);

            // Stop tracking it: the context lives for the whole request and doesn't need it any more.
            _db.Entry(run).State = EntityState.Detached;
        }

        public Task<AgentRunRecord> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return _db.AgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<AgentRunPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            int safePage = Math.Max(1, page);
            int safeSize = Math.Min(Math.Max(1, pageSize), MaxPageSize);

            int total = await _db.AgentRuns.CountAsync(cancellationToken);
            List<AgentRunRecord> items = await _db.AgentRuns.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync(cancellationToken);

            return new AgentRunPage(items, total, safePage, safeSize);
        }

        public async Task<AgentRunStats> GetStatsAsync(CancellationToken cancellationToken)
        {
            var stats = new AgentRunStats();

            // The totals are added up by the database in one query.
            var totals = await _db.AgentRuns
                .GroupBy(r => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Blocked = g.Count(r => r.Blocked),
                    Errors = g.Count(r => r.Error != null),
                    AverageMs = g.Average(r => r.DurationMs),
                    InputTokens = g.Sum(r => (long)r.InputTokens),
                    OutputTokens = g.Sum(r => (long)r.OutputTokens),
                    Cost = g.Sum(r => r.EstimatedCostUsd)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (totals == null)
                return stats; // no runs yet

            stats.TotalRuns = totals.Count;
            stats.BlockedRuns = totals.Blocked;
            stats.ErrorRuns = totals.Errors;
            stats.AverageDurationMs = Math.Round(totals.AverageMs, 1);
            stats.BlockRate = Math.Round((double)totals.Blocked / totals.Count, 3);
            stats.TotalInputTokens = totals.InputTokens;
            stats.TotalOutputTokens = totals.OutputTokens;
            stats.TotalEstimatedCostUsd = totals.Cost;

            // The tool lists are stored as JSON, so they are counted here in C#.
            // Only the ToolsUsed column is loaded, and retention keeps the table small.
            List<List<string>> toolLists = await _db.AgentRuns.AsNoTracking()
                .Select(r => r.ToolsUsed)
                .ToListAsync(cancellationToken);

            stats.TopTools = toolLists
                .Where(list => list != null)
                .SelectMany(list => list)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ToolUsageCount(g.Key, g.Count()))
                .OrderByDescending(t => t.Count)
                .Take(TopToolCount)
                .ToList();

            return stats;
        }
    }
}
