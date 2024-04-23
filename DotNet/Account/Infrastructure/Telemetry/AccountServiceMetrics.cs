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
            AccountDeactivatedCounter = meter.CreateCounter<long>("link_account_service.account_deactivated.count"); 
            AccountRestoredCounter = meter.CreateCounter<long>("link_account_service.account_restored.count");
        }

        public Counter<long> AccountAddedCounter { get; private set; }
        public void IncrementAccountAddedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountAddedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountDeactivatedCounter { get; private set; }
        public void IncrementAccountDeactivatedCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountDeactivatedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> AccountRestoredCounter { get; private set; }
        public void IncrementAccountRestoredCounter(List<KeyValuePair<string, object?>> tags)
        {
            AccountRestoredCounter.Add(1, tags.ToArray());
        }
    }
}
