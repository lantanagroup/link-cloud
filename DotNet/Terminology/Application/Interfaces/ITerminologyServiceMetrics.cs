namespace LantanaGroup.Link.Terminology.Application.Interfaces;

public interface ITerminologyServiceMetrics
{
    void IncrementLookupCount(string outcome, string groupKind);
    void RecordLookupDuration(double durationMilliseconds, string groupKind, string cache);
}
