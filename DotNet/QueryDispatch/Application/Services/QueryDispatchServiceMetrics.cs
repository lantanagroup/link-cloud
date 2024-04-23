using QueryDispatch.Application.Interfaces;
using QueryDispatch.Application.Settings;
using System.Diagnostics.Metrics;

namespace QueryDispatch.Application.Services
{
    public class QueryDispatchServiceMetrics : IQueryDispatchServiceMetrics
    {
        public const string MeterName = $"Link.{QueryDispatchConstants.ServiceName}";

        public QueryDispatchServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
        }
    }
}
