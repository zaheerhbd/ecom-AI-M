using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Evals;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    /// <summary>
    /// Saves eval runs (EvalRuns table) and their case results (EvalCaseResults table).
    /// The eval runner saves a run many times while it progresses, so SaveRunAsync adds the
    /// row the first time and updates it after that.
    /// </summary>
    public class EfEvalRunStore : IEvalRunStore
    {
        private const int MaxPageSize = 100;

        private readonly StoreContext _db;

        public EfEvalRunStore(StoreContext db)
        {
            _db = db;
        }

        public async Task SaveRunAsync(EvalRunRecord run, CancellationToken cancellationToken)
        {
            bool exists = await _db.EvalRuns.AnyAsync(r => r.Id == run.Id, cancellationToken);
            if (exists)
                _db.EvalRuns.Update(run);
            else
                _db.EvalRuns.Add(run);

            await _db.SaveChangesAsync(cancellationToken);
            _db.Entry(run).State = EntityState.Detached;
        }

        public async Task SaveCaseResultAsync(EvalCaseResultRecord result, CancellationToken cancellationToken)
        {
            _db.EvalCaseResults.Add(result);
            await _db.SaveChangesAsync(cancellationToken);
            _db.Entry(result).State = EntityState.Detached;
        }

        public Task<EvalRunRecord> GetRunAsync(Guid id, CancellationToken cancellationToken)
        {
            return _db.EvalRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<EvalRunPage> ListRunsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            int safePage = Math.Max(1, page);
            int safeSize = Math.Min(Math.Max(1, pageSize), MaxPageSize);

            int total = await _db.EvalRuns.CountAsync(cancellationToken);
            List<EvalRunRecord> items = await _db.EvalRuns.AsNoTracking()
                .OrderByDescending(r => r.StartedAt)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync(cancellationToken);

            return new EvalRunPage(items, total, safePage, safeSize);
        }

        public Task<List<EvalCaseResultRecord>> GetCaseResultsAsync(Guid evalRunId, CancellationToken cancellationToken)
        {
            return _db.EvalCaseResults.AsNoTracking()
                .Where(r => r.EvalRunId == evalRunId)
                .OrderBy(r => r.Id) // the database numbers rows in the order they were saved
                .ToListAsync(cancellationToken);
        }
    }
}
