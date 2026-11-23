using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Evals
{
    /// <summary>
    /// Keeps eval runs in memory. Used by the tests, the command-line gate, and as the default
    /// until the shop plugs in its database store. Lost on restart.
    /// </summary>
    public sealed class InMemoryEvalRunStore : IEvalRunStore
    {
        private readonly object _lock = new object();
        private readonly List<EvalRunRecord> _runs = new List<EvalRunRecord>(); // oldest first
        private readonly List<EvalCaseResultRecord> _results = new List<EvalCaseResultRecord>();
        private int _nextResultId = 1;

        /// <summary>Adds the run, or replaces the saved copy with the same id.</summary>
        public Task SaveRunAsync(EvalRunRecord run, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                int index = _runs.FindIndex(existing => existing.Id == run.Id);
                if (index >= 0)
                    _runs[index] = run;
                else
                    _runs.Add(run);
            }

            return Task.CompletedTask;
        }

        /// <summary>Adds one case result and gives it an id.</summary>
        public Task SaveCaseResultAsync(EvalCaseResultRecord result, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                result.Id = _nextResultId++;
                _results.Add(result);
            }

            return Task.CompletedTask;
        }

        /// <summary>Returns one run, or null.</summary>
        public Task<EvalRunRecord> GetRunAsync(Guid id, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult(_runs.Find(run => run.Id == id));
            }
        }

        /// <summary>Returns one page of runs, newest first.</summary>
        public Task<EvalRunPage> ListRunsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            int safePage = Math.Max(1, page);
            int safeSize = Math.Min(Math.Max(1, pageSize), 100);
            List<EvalRunRecord> items = new List<EvalRunRecord>();
            int total;

            lock (_lock)
            {
                total = _runs.Count;
                int skip = (safePage - 1) * safeSize;
                for (int i = _runs.Count - 1 - skip; i >= 0 && items.Count < safeSize; i--)
                    items.Add(_runs[i]);
            }

            return Task.FromResult(new EvalRunPage(items, total, safePage, safeSize));
        }

        /// <summary>Returns every case result of one run, in the order they ran.</summary>
        public Task<List<EvalCaseResultRecord>> GetCaseResultsAsync(Guid evalRunId, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult(_results.FindAll(result => result.EvalRunId == evalRunId));
            }
        }
    }
}
