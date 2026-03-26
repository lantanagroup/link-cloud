using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Account.Infrastructure.Telemetry
{
    public class AccountServiceMetrics : IAccountServiceMetrics
    {
        public const string MeterName = $"Link.{AccountConstants.ServiceName}";

        public AccountServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            AccountAddedCounter = meter.CreateCounter<long>("link_account_service.account_added.count");
            AccountActiviatedCounter = meter.CreateCounter<long>("link_account_service.account_activiated.count");
            AccountDeactivatedCounter = meter.CreateCounter<long>("link_account_service.account_deactivated.count"); 
            AccountDeletedCounter = meter.CreateCounter<long>("link_account_service.account_deleted.count");
            AccountRestoredCounter = meter.CreateCounter<long>("link_account_service.account_restored.count");
        }

        public Counter<long> AccountAddedCounter { get; private set; }
        public void IncrementAccountAddedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountAddedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountActiviatedCounter { get; private set; }
        public void IncrementAccountActiviatedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountActiviatedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountDeactivatedCounter { get; private set; }
        public void IncrementAccountDeactivatedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountDeactivatedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountDeletedCounter { get; private set; }
        public void IncrementAccountDeletedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountDeletedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountRestoredCounter { get; private set; }
        public void IncrementAccountRestoredCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountRestoredCounter.Add(1, tags.ToArray());
        }
    }
}
