namespace LantanaGroup.Link.Report.Application.Interfaces
{
    public interface IReportServiceMetrics
    {
        void IncrementReportGeneratedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementStatusTransition(string facilityId, string from, string to);
        void RecordPersistDuration(string facilityId, double durationMilliseconds);
    }
}
