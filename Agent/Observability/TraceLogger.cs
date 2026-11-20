using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Agent.Observability
{
    /// <summary>
    /// Prints every finished span to the log so you can watch traces in the console:
    ///   [trace 4bf9...] execute_tool search_products took 4.2 ms (Ok)
    /// It's a stand-in for a real tracing backend (Application Insights, Jaeger, Aspire Dashboard).
    /// It only starts when something asks for it (resolves it from DI), so it is off by default.
    /// </summary>
    public sealed class TraceLogger : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ILogger<TraceLogger> _logger;

        /// <summary>Starts listening to the agent's spans straight away.</summary>
        public TraceLogger(ILogger<TraceLogger> logger)
        {
            _logger = logger;
            _listener = new ActivityListener();

            // Only listen to OUR spans, not every library's.
            _listener.ShouldListenTo = source => source.Name == AgentTelemetry.Name;

            // "AllData" means: yes, record these spans (this is also where sampling would be configured).
            _listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;

            _listener.ActivityStopped = LogFinishedSpan;
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>Writes one log line for a finished span.</summary>
        private void LogFinishedSpan(Activity activity)
        {
            _logger.LogInformation(
                "[trace {TraceId}] {Span} took {Ms:F1} ms ({Status})",
                activity.TraceId.ToHexString().Substring(0, 8), activity.DisplayName, activity.Duration.TotalMilliseconds, activity.Status);
        }

        /// <summary>Stops listening.</summary>
        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
