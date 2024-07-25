namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka
{
    public class DataAcquiredMessage
    {
        public string CorrelationId { get; set; }
        public List<DataAcquisitionTypes> Type { get; set; }
        public string ReportMonth { get; set; }
        public string ReportYear { get; set; }
        public string TenantId { get; set; }
        public object Data { get; set; }
    }
}
