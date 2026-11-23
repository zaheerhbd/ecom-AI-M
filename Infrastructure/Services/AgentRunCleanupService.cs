using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// Deletes agent runs older than Agent:RunRetentionDays (default 30, decision D3).
    ///
    /// It is a BACKGROUND SERVICE: ASP.NET Core starts it with the app. It cleans up once shortly
    /// after startup, then once a day. Runs hold questions users typed, so we don't keep them forever.
    /// </summary>
    public class AgentRunCleanupService : BackgroundService
    {
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<AgentRunCleanupService> _logger;

        public AgentRunCleanupService(IServiceScopeFactory scopes, IConfiguration config, ILogger<AgentRunCleanupService> logger)
        {
            _scopes = scopes;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Wait a little, so cleanup never slows down the app starting.
                await Task.Delay(StartDelay, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await DeleteOldRunsAsync(stoppingToken);
                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The app is shutting down: nothing to do.
            }
        }

        /// <summary>Deletes the old runs. Returns how many were deleted. Errors are logged, never thrown.</summary>
        public async Task<int> DeleteOldRunsAsync(CancellationToken cancellationToken)
        {
            int days = _config.GetValue("Agent:RunRetentionDays", 30);
            if (days < 1)
                return 0; // 0 or less switches cleanup off

            try
            {
                // StoreContext is scoped, so a background service makes its own scope.
                using (var scope = _scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<StoreContext>();
                    DateTime cutoff = DateTime.UtcNow.AddDays(-days);

                    // ExecuteDeleteAsync runs one DELETE statement in the database (no loading first).
                    int deleted = await db.AgentRuns.Where(r => r.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
                    if (deleted > 0)
                        _logger.LogInformation("Deleted {Count} agent runs older than {Days} days.", deleted, days);
                    return deleted;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogWarning(ex, "Could not delete old agent runs. Will try again tomorrow.");
                return 0;
            }
        }
    }
}
