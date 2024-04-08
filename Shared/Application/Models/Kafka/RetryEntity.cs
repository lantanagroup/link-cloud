using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Models
{
    [BsonCollection("retryEntity")]
    [BsonIgnoreExtraElements]
    public class RetryEntity : BaseEntity
    {
        public string ServiceName { get; set; }
        public string FacilityId { get; set; }
        public string Topic { get; set; }
        public string Key { get; set; }
        public string Value { get; set; } 
        public Dictionary<string, string> Headers { get; set; }
        public string ScheduledTrigger { get; set; }
        public int RetryCount { get; set; }
        public string CorrelationId { get; set; }
        public DateTime CreateDate { get; set; }

        [BsonIgnore]
        public string JobId => $"{Id?.ToString() ?? string.Empty}-{FacilityId ?? string.Empty}-{Topic ?? string.Empty}";
    }
}