using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Shared.Application.Models
{
    public class RetryModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ServiceName { get; set; }
        public string FacilityId { get; set; }
        public string Topic { get; set; }
        public string Key { get; set; }
        public string Value { get; set; } 
        public Dictionary<string, string> Headers { get; set; }
        public DateTime ScheduledTrigger { get; set; }
        public int RetryCount { get; set; } = 1;
        public string CorrelationId { get; set; }
        public string JobId => $"{Id}-{FacilityId}-{Topic}";
    }
}