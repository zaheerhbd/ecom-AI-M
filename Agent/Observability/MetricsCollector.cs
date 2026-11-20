using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Agent.Observability
{
    /// <summary>
    /// A tiny "dashboard in memory". It listens to the agent's metrics and keeps running totals
    /// since the app started. In production you would use OpenTelemetry + Application Insights
    /// instead, but this lets you SEE metrics with zero setup.
    /// </summary>
    public sealed class MetricsCollector : IDisposable
    {
        /// <summary>
        /// Running totals for one histogram (e.g. latency). It never changes after creation:
        /// adding a value returns a NEW object, which keeps ConcurrentDictionary updates thread-safe.
        /// </summary>
        private sealed class HistogramTotals
        {
            public HistogramTotals(long count, double sum, double max)
            {
                Count = count;
                Sum = sum;
                Max = max;
            }

            public long Count { get; }
            public double Sum { get; }
            public double Max { get; }

            public HistogramTotals Add(double value)
            {
                return new HistogramTotals(Count + 1, Sum + value, Math.Max(Max, value));
            }
        }

        private readonly MeterListener _listener = new MeterListener();
        private readonly ConcurrentDictionary<string, double> _totals = new ConcurrentDictionary<string, double>();
        private readonly ConcurrentDictionary<string, HistogramTotals> _histograms = new ConcurrentDictionary<string, HistogramTotals>();

        /// <summary>Starts listening to the agent's metrics straight away.</summary>
        public MetricsCollector()
        {
            // Only listen to OUR meter, not every meter in .NET.
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentTelemetry.Name)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Add(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Add(instrument, value, tags));
            _listener.Start();
        }

        /// <summary>Adds one measurement to the running totals.</summary>
        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            // Time histograms keep count, sum and max so we can show an average.
            if (instrument is Histogram<double>)
            {
                _histograms.AddOrUpdate(instrument.Name,
                    new HistogramTotals(1, value, value),
                    (name, existing) => existing.Add(value));
                return;
            }

            // Counters (and the token histogram) just keep a running total.
            _totals.AddOrUpdate(instrument.Name, value, (name, existing) => existing + value);

            // Also keep a total per tag, e.g. "agent.tool_calls{tool=search_products}".
            foreach (KeyValuePair<string, object> tag in tags)
            {
                string key = instrument.Name + "{" + tag.Key + "=" + tag.Value + "}";
                _totals.AddOrUpdate(key, value, (name, existing) => existing + value);
            }
        }

        /// <summary>A point-in-time copy of all numbers, safe to return as JSON.</summary>
        public object Snapshot()
        {
            // Counters, sorted by name so the output is easy to scan.
            SortedDictionary<string, double> counters = new SortedDictionary<string, double>(_totals);

            // Latency: turn the running totals into count / average / max.
            Dictionary<string, object> latency = new Dictionary<string, object>();
            foreach (KeyValuePair<string, HistogramTotals> entry in _histograms)
            {
                HistogramTotals totals = entry.Value;
                double average = totals.Count == 0 ? 0 : Math.Round(totals.Sum / totals.Count, 3);
                latency[entry.Key] = new { count = totals.Count, average = average, max = Math.Round(totals.Max, 3) };
            }

            return new { counters = counters, latency = latency };
        }

        /// <summary>Stops listening.</summary>
        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
