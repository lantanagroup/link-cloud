using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Telemetry
{
    public class LinkAdminMetrics : ILinkAdminMetrics
    {
        public const string MeterName = $"Link.{LinkAdminConstants.ServiceName}";

        public LinkAdminMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            UserLoginCounter = meter.CreateCounter<long>("link_admin.user_login.count");
            FailedAuthenticationCounter = meter.CreateCounter<long>("link_admin.failed_authentication.count");
            TokenGeneratedCounter = meter.CreateCounter<long>("link_admin.token_generated.count");
            TokenKeyRefreshCounter = meter.CreateCounter<long>("link_admin.token_key_refresh.count");

        }

        public Counter<long> FailedAuthenticationCounter { get; private set; }
        public void IncrementFailedAuthenticationCounter(List<KeyValuePair<string, object?>> tags)
        {
            FailedAuthenticationCounter.Add(1, tags.ToArray());
        }

        public Counter<long> TokenGeneratedCounter { get; private set; }
        public void IncrementTokenGeneratedCounter(List<KeyValuePair<string, object?>> tags)
        {
            TokenGeneratedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> TokenKeyRefreshCounter { get; private set; }
        public void IncrementTokenKeyRefreshCounter(List<KeyValuePair<string, object?>> tags)
        {
            TokenKeyRefreshCounter.Add(1, tags.ToArray());
        }

        public Counter<long> UserLoginCounter { get; private set; }
        public void IncrementUserLoginCounter(List<KeyValuePair<string, object?>> tags)
        {
            UserLoginCounter.Add(1, tags.ToArray());
        }
    }
}
