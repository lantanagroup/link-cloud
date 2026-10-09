namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public static class ProduceRateSamples
{
    public static (bool Known, double PerSecond) Measure(IReadOnlyList<WatermarkSample> samples)
    {
        if (samples.Count < 2)
            return (false, 0);

        var first = samples[0];
        var last = samples[^1];
        var seconds = (last.At - first.At).TotalSeconds;
        if (seconds <= 0)
            return (false, 0);

        return (true, Math.Max(0, (last.Sum - first.Sum) / seconds));
    }
}
