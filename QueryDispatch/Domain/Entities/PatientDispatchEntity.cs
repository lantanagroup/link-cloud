namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    [BsonCollection("patientDispatches")]
    public class PatientDispatchEntity : BaseQueryEntity
    {
        public string PatientId { get; set; }
        public DateTime TriggerDate {get;set;}
        public List<ReportPeriodEntity> ScheduledReportPeriods { get; set; }
        public string CorrelationId { get; set; }
    }
}
