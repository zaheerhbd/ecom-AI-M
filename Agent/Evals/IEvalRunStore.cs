using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Evals
{
    /// <summary>
    /// Where eval runs and their case results are saved. The runner saves progress after every
    /// case, so the evals page can show a progress bar while a run is going.
    /// The engine ships an in-memory version; the shop adds a database version in Phase 3.
    /// </summary>
    public interface IEvalRunStore
    {
        /// <summary>Adds or updates an eval run (it is saved many times as it progresses).</summary>
        Task SaveRunAsync(EvalRunRecord run, CancellationToken cancellationToken);

        /// <summary>Adds the result of one case.</summary>
        Task SaveCaseResultAsync(EvalCaseResultRecord result, CancellationToken cancellationToken);

        /// <summary>Returns one eval run, or null.</summary>
        Task<EvalRunRecord> GetRunAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Returns one page of eval runs, newest first. Page numbers start at 1.</summary>
        Task<EvalRunPage> ListRunsAsync(int page, int pageSize, CancellationToken cancellationToken);

        /// <summary>Returns every case result of one eval run, in the order they ran.</summary>
        Task<List<EvalCaseResultRecord>> GetCaseResultsAsync(Guid evalRunId, CancellationToken cancellationToken);
    }
}
