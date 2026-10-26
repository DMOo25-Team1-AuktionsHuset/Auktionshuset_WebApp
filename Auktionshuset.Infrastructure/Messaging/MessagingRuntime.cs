using Auktionshuset.Application.Abstraction;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace Auktionshuset.Infrastructure.Messaging
{
    internal class MessagingRuntime
        : IMessagingDiagnostics, IDisposable
    {
        public const string MeterName = "Auktionshuset.Messaging";

        private readonly object _gate = new();
        private readonly TimeProvider _clock;

        private readonly Meter _meter = new(MeterName);

        private readonly Counter<long> _attempts;
        private readonly Counter<long> _completed;
        private readonly Counter<long> _failed;
        private readonly Counter<long> _leaseLost;
        private readonly Counter<long> _replayed;
        private readonly Counter<long> _cleaned;

        private readonly Histogram<double> _duration;

        private MessagingDiagnostics _view;

        public MessagingRuntime(
            TimeProvider clock,
            IOptions<MessagingOperationsOptions> options)
        {
            _clock = clock;

            _view = new MessagingDiagnostics(
                InstanceId: Guid.NewGuid().ToString("N"),
                WorkersEnabled: options.Value.RunWorkers,
                StartedAtTime: clock.GetUtcNow().UtcDateTime,
                Status: null,
                SampleError: null,
                OutboxDbActivity: null,
                InboxDbActivity: null,
                MaintenanceDbActivity: null,
                ConsumerActive: false,
                LastReceivedAtTime: null,
                OutboxLastCompletedAtTime: null,
                InboxLastCompletedAtTime: null);

            _attempts = _meter.CreateCounter<long>("messaging.attempts");
            _completed = _meter.CreateCounter<long>("messaging.completed");
            _failed = _meter.CreateCounter<long>("messaging.failed");
            _leaseLost = _meter.CreateCounter<long>("messaging.lease_lost");
            _replayed = _meter.CreateCounter<long>("messaging.replayed");
            _cleaned = _meter.CreateCounter<long>("messaging.outbox.cleaned");

            _duration = _meter.CreateHistogram<double>(
                "messaging.processing.duration",
                unit: "s");

            _meter.CreateObservableGauge<long>(
                "messaging.pending",
                () => Observe(x => x.Pending));

            _meter.CreateObservableGauge<long>(
                "messaging.ready",
                () => Observe(x => x.Ready));

            _meter.CreateObservableGauge<long>(
                "messaging.quarantined",
                () => Observe(x => x.Quarantined));

            _meter.CreateObservableGauge<double>(
                "messaging.oldest_ready.age",
                ObserveOldestReadyAge,
                unit: "s");

            _meter.CreateObservableGauge<double>(
                "messaging.status.sample_age",
                () =>
                {
                    MessagingStatus? status = Read().Status;

                    return status is null
                        ? Array.Empty<Measurement<double>>()
                        : new[]
                        {
                        new Measurement<double>(
                            Math.Max(0,
                                (_clock.GetUtcNow().UtcDateTime
                                 - status.SampledAtTime).TotalSeconds))
                        };
                },
                unit: "s");
        }

        public MessagingDiagnostics Read()
        {
            lock (_gate)
                return _view;
        }

        public void Sampled(MessagingStatus status)
        {
            lock (_gate)
                _view = _view with { Status = status, SampleError = null };
        }

        public void SampleFailed(Exception exception)
        {
            lock (_gate)
                _view = _view with
                {
                    SampleError = exception.GetType().Name
                };
        }

        public void Touch(string worker)
        {
            DateTime now = _clock.GetUtcNow().UtcDateTime;

            lock (_gate)
            {
                _view = worker switch
                {
                    "outbox" => _view with { OutboxDbActivity = now },
                    "inbox" => _view with { InboxDbActivity = now },
                    "maintenance" => _view with { MaintenanceDbActivity = now },
                    _ => throw new ArgumentOutOfRangeException(nameof(worker))
                };
            }
        }

        public void SetConsumerActive(bool active)
        {
            lock (_gate)
                _view = _view with { ConsumerActive = active };
        }

        public void Received()
        {
            lock (_gate)
                _view = _view with
                {
                    LastReceivedAtTime = _clock.GetUtcNow().UtcDateTime
                };
        }

        public void Attempt(string pipeline)
            => _attempts.Add(1, Tag(pipeline));

        public void Failed(string pipeline)
            => _failed.Add(1, Tag(pipeline));

        public void LeaseLost(string pipeline)
            => _leaseLost.Add(1, Tag(pipeline));

        public void Replayed(string pipeline)
            => _replayed.Add(1, Tag(pipeline));

        public void Cleaned(int count)
            => _cleaned.Add(count);

        public void Completed(string pipeline)
        {
            _completed.Add(1, Tag(pipeline));

            DateTime now = _clock.GetUtcNow().UtcDateTime;

            lock (_gate)
            {
                _view = pipeline switch
                {
                    "outbox" => _view with { OutboxLastCompletedAtTime = now },
                    "inbox" => _view with { InboxLastCompletedAtTime = now },
                    _ => throw new ArgumentOutOfRangeException(nameof(pipeline))
                };
            }
        }

        public IDisposable Measure(string pipeline)
            => new DurationMeasurement(_duration, pipeline);

        private IEnumerable<Measurement<long>> Observe(
            Func<QueueStatus, long> selector)
        {
            MessagingStatus? status = Read().Status;

            return status is null
                ? Array.Empty<Measurement<long>>()
                : status.Queues
                    .Select(x => new Measurement<long>(
                        selector(x),
                        Tag(x.Pipeline)))
                    .ToArray();
        }

        private IEnumerable<Measurement<double>> ObserveOldestReadyAge()
        {
            MessagingStatus? status = Read().Status;

            if (status is null)
                return Array.Empty<Measurement<double>>();

            DateTime now = _clock.GetUtcNow().UtcDateTime;

            return status.Queues
                .Select(x => new Measurement<double>(
                    x.OldestReadyAtTime is DateTime oldest
                        ? Math.Max(0, (now - oldest).TotalSeconds)
                        : 0,
                    Tag(x.Pipeline)))
                .ToArray();
        }

        private static KeyValuePair<string, object?> Tag(string pipeline)
            => new("pipeline", pipeline);

        public void Dispose() => _meter.Dispose();

        private sealed class DurationMeasurement(
            Histogram<double> histogram,
            string pipeline) : IDisposable
        {
            private readonly long _started = Stopwatch.GetTimestamp();

            public void Dispose()
            {
                histogram.Record(
                    Stopwatch.GetElapsedTime(_started).TotalSeconds,
                    Tag(pipeline));
            }
        }
    }
}
