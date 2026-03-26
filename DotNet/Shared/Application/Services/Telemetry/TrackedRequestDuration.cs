using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Shared.Application.Services.Telemetry
{
    public class TrackedRequestDuration : IDisposable
    {
        private readonly TimeProvider _timeProvider;
        private readonly long _requestStartTime;
        private readonly Histogram<double> _histogram;
        private readonly List<KeyValuePair<string, object?>> _tags;

        public TrackedRequestDuration(Histogram<double> histogram, TimeProvider timeProvider, List<KeyValuePair<string, object?>> tags)
        {
            _histogram = histogram;
            _timeProvider = timeProvider;
            _requestStartTime = timeProvider.GetTimestamp();
            _tags = tags;
        }

        public void Dispose()
        {
            var elapsed = _timeProvider.GetElapsedTime(_requestStartTime);
            _histogram.Record(elapsed.TotalMilliseconds, _tags.ToArray());
          
        }
    }
}
