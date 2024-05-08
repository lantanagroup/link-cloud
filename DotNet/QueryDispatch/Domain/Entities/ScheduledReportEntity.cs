using System.ComponentModel.DataAnnotations.Schema;
namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [Table("scheduledReports")]
    public class ScheduledReportEntity : BaseQueryEntity
    {
        public List<ReportPeriodEntity> ReportPeriods { get; set; }
    }
}
