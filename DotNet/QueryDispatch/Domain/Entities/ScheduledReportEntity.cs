namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [BsonCollection("scheduledReports")]
    public class ScheduledReportEntity : BaseQueryEntity
    {
        public List<ReportPeriodEntity> ReportPeriods { get; set; }
    }
}
