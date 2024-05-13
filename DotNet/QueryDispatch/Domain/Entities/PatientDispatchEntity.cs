using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [Table("patientDispatches")]
    public class PatientDispatchEntity : BaseQueryEntity
    {
        public string PatientId { get; set; }
        public DateTime TriggerDate {get;set;}
        public List<ReportPeriodEntity> ScheduledReportPeriods { get; set; }
        public string CorrelationId { get; set; }
    }
}
