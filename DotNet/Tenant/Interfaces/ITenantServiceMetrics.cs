namespace LantanaGroup.Link.Tenant.Interfaces
{
    public interface ITenantServiceMetrics
    {
        void IncrementReportScheduledCounter(List<KeyValuePair<string, object?>> tags);
    }
}
