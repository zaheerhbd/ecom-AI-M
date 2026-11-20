using System;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Runs
{
    /// <summary>
    /// Where finished runs are saved, so you can look at them later (the runs page).
    /// The engine ships an in-memory version; the shop adds a database version in Phase 3.
    /// </summary>
    public interface IAgentRunStore
    {
        /// <summary>Saves one finished run.</summary>
        Task SaveAsync(AgentRunRecord run, CancellationToken cancellationToken);

        /// <summary>Returns one run, or null if there is no run with this id.</summary>
        Task<AgentRunRecord> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Returns one page of runs, newest first. Page numbers start at 1.</summary>
        Task<AgentRunPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

        /// <summary>Returns totals across all runs.</summary>
        Task<AgentRunStats> GetStatsAsync(CancellationToken cancellationToken);
    }
}
