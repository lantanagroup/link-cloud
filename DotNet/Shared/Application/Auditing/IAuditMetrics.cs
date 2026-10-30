using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// How a lost audit event becomes visible. Until the outbox feature lands, a produce that fails
    /// after the handler's change has committed is gone: the request succeeded, the database changed,
    /// and nothing recorded who did it. This counter is the only signal that happened, so every
    /// service story's QA actions check it with Kafka stopped.
    /// </summary>
    public interface IAuditMetrics
    {
        /// <summary>
        /// One per audit event abandoned after every retry was spent.
        /// </summary>
        void PublishFailed(string? resource);
    }

    /// <inheritdoc cref="IAuditMetrics"/>
    public sealed class AuditMetrics : IAuditMetrics
    {
        /// <summary>
        /// The name the story and every service story's QA actions use. It deliberately does not
        /// follow the link_(service).(thing).count convention of the other Link counters: this one is
        /// the same measure in all of them, and a per-service name would need a different dashboard
        /// query for each.
        /// </summary>
        public const string PublishFailedCounter = "audit_publish_failed";

        private readonly Counter<long> _publishFailed;

        // The host's meter, so this exports through the host's existing OpenTelemetry registration.
        // TelemetryServiceExtension subscribes to exactly one meter, Link.{name}, where name is the
        // constant the host passes to AddLinkTelemetry and SetupServiceInformation stores in
        // ServiceConfigName. A meter built from ServiceName - the display name from configuration -
        // is never exported, so a blank ServiceConfigName would silently lose the one counter that
        // reports lost audit events. Fail at startup instead.
        public AuditMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);
            ArgumentNullException.ThrowIfNull(serviceInformation);
            if (string.IsNullOrWhiteSpace(serviceInformation.ServiceConfigName))
            {
                throw new ArgumentException(
                    $"{nameof(ServiceInformation)}.{nameof(ServiceInformation.ServiceConfigName)} is required; " +
                    "it names the meter the host exports.", nameof(serviceInformation));
            }

            var meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            _publishFailed = meter.CreateCounter<long>(PublishFailedCounter);
        }

        public void PublishFailed(string? resource) =>
            _publishFailed.Add(1, new KeyValuePair<string, object?>("resource", resource ?? "unknown"));
    }
}
