namespace LantanaGroup.Link.Report.Application.Interfaces
{
    public interface IReportServiceMetrics
    {
        void IncrementReportGeneratedCounter(List<KeyValuePair<string, object?>> tags);        
    }
}
