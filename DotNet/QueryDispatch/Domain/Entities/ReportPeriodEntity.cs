namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    public class ReportPeriodEntity
    {
        public List<string> ReportTypes { get; set; }
        public string Frequency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string CorrelationId { get; set; }
    }
}