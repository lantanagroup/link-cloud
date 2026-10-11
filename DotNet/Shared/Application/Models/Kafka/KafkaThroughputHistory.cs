namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>One topic family's high-watermark sums and subscribed lag at one sample.</summary>
public sealed record KafkaTopicSample(string Topic, long MainSum, long RetrySum, long ErrorSum, long Lag);

public sealed class KafkaHistoryPoint
{
    public DateTimeOffset At { get; set; }
    public double InPerSecond { get; set; }
    public double OutPerSecond { get; set; }
    public long TotalLag { get; set; }
}

public sealed class KafkaTopicRate
{
    public string Topic { get; set; } = "";
    public double PerSecond { get; set; }
}

public sealed class KafkaLandedRow
{
    public string Topic { get; set; } = "";
    public long Retry { get; set; }
    public long Error { get; set; }
}

public sealed class KafkaHistoryResponse
{
    public int WindowMinutes { get; set; }
    public List<KafkaHistoryPoint> Points { get; set; } = [];
    public List<KafkaTopicRate> Busiest { get; set; } = [];
    public List<KafkaLandedRow> Landed { get; set; } = [];
    public bool LandedKnown { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// A small bounded ring of cluster samples for the Kafka overview. Each frame holds one row per
/// catalog topic family, frames are at least <see cref="MinSpacing"/> apart, and the ring keeps
/// at most <see cref="Capacity"/> frames and never more than the window. Nothing is persisted.
/// </summary>
public sealed class KafkaThroughputHistory
{
    public static KafkaThroughputHistory Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Queue<(DateTimeOffset At, IReadOnlyList<KafkaTopicSample> Topics)> _frames = new();

    public KafkaThroughputHistory(int capacity = 120, TimeSpan? minSpacing = null, TimeSpan? window = null)
    {
        Capacity = Math.Clamp(capacity, 2, 720);
        MinSpacing = minSpacing ?? TimeSpan.FromSeconds(30);
        Window = window ?? TimeSpan.FromHours(1);
    }

    public int Capacity { get; }
    public TimeSpan MinSpacing { get; }
    public TimeSpan Window { get; }
    public const int MaxTopicsPerFrame = 200;

    public int Count { get { lock (_gate) return _frames.Count; } }

    public bool Record(DateTimeOffset at, IEnumerable<KafkaTopicSample> topics)
    {
        var rows = topics.Take(MaxTopicsPerFrame).ToList();
        lock (_gate)
        {
            if (_frames.Count > 0 && at - _frames.Last().At < MinSpacing)
                return false;
            _frames.Enqueue((at, rows));
            while (_frames.Count > Capacity || (_frames.Count > 0 && at - _frames.Peek().At > Window))
                _frames.Dequeue();
            return true;
        }
    }

    public KafkaHistoryResponse Snapshot(int top = 5)
    {
        top = Math.Clamp(top, 1, 20);
        List<(DateTimeOffset At, IReadOnlyList<KafkaTopicSample> Topics)> frames;
        lock (_gate)
            frames = _frames.ToList();

        var response = new KafkaHistoryResponse { WindowMinutes = (int)Window.TotalMinutes };
        for (var i = 1; i < frames.Count; i++)
        {
            var seconds = (frames[i].At - frames[i - 1].At).TotalSeconds;
            if (seconds <= 0)
                continue;
            var before = frames[i - 1].Topics.ToDictionary(row => row.Topic, StringComparer.Ordinal);
            double produced = 0;
            foreach (var row in frames[i].Topics)
                if (before.TryGetValue(row.Topic, out var old))
                    produced += Math.Max(0, row.MainSum - old.MainSum);
            var lagBefore = frames[i - 1].Topics.Sum(row => row.Lag);
            var lagNow = frames[i].Topics.Sum(row => row.Lag);
            var inRate = produced / seconds;
            response.Points.Add(new KafkaHistoryPoint
            {
                At = frames[i].At,
                InPerSecond = inRate,
                OutPerSecond = Math.Max(0, inRate - (lagNow - lagBefore) / seconds),
                TotalLag = lagNow
            });
        }

        if (frames.Count >= 2)
        {
            var last = frames[^1];
            var prev = frames[^2].Topics.ToDictionary(row => row.Topic, StringComparer.Ordinal);
            var seconds = Math.Max(1, (last.At - frames[^2].At).TotalSeconds);
            response.Busiest = last.Topics
                .Where(row => prev.ContainsKey(row.Topic))
                .Select(row => new KafkaTopicRate { Topic = row.Topic, PerSecond = Math.Max(0, row.MainSum - prev[row.Topic].MainSum) / seconds })
                .Where(row => row.PerSecond > 0)
                .OrderByDescending(row => row.PerSecond).ThenBy(row => row.Topic, StringComparer.Ordinal)
                .Take(top).ToList();

            var first = frames[0].Topics.ToDictionary(row => row.Topic, StringComparer.Ordinal);
            response.LandedKnown = true;
            response.Landed = last.Topics
                .Where(row => first.ContainsKey(row.Topic))
                .Select(row => new KafkaLandedRow
                {
                    Topic = row.Topic,
                    Retry = Math.Max(0, row.RetrySum - first[row.Topic].RetrySum),
                    Error = Math.Max(0, row.ErrorSum - first[row.Topic].ErrorSum)
                })
                .Where(row => row.Retry + row.Error > 0)
                .OrderByDescending(row => row.Error).ThenByDescending(row => row.Retry).ThenBy(row => row.Topic, StringComparer.Ordinal)
                .Take(top).ToList();
        }

        return response;
    }
}
