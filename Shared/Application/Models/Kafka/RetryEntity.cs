using Confluent.Kafka;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class RetryEntity
    {
        public Guid? Id { get; set; }
        public string? ServiceName { get; set; }
        public string? ClientId { get; set; }

        public string? FacilityId { get; set; }
        public string? Topic { get; set; }
        public long Offset { get; set; }
        public int Partition { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
        public Headers? Headers { get; set; }    
        public string? ScheduledTrigger { get; set; }
        public int RetryCount { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime CreateDate { get; set; }


        public string JobId => $"{Id?.ToString() ?? string.Empty}-{FacilityId ?? string.Empty}-{Topic ?? string.Empty}";
    }
}