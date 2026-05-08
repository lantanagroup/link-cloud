using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Interfaces;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Tenant.Services
{
    public class TenantServiceMetrics : ITenantServiceMetrics
    {
        public const string MeterName = $"Link.{TenantConstants.ServiceName}";        

        public TenantServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            ReportScheduledCounter = meter.CreateCounter<long>("link_tenant_service.report_scheduled.count");
        }

        private readonly Counter<long> ReportScheduledCounter;
        public void IncrementReportScheduledCounter(List<KeyValuePair<string, object?>> tags)
        {
            ReportScheduledCounter.Add(1, tags.ToArray());
        }
    }
}
