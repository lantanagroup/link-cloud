using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Shared.Application.Models
{
    [BsonCollection("eventRetries")]
    [BsonIgnoreExtraElements]
    [Table("EventRetries")]
    public class RetryEntity : BaseEntity
    {
        public string ServiceName { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public DateTime ScheduledTrigger { get; set; }
        public int RetryCount { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime CreateDate { get; set; }

        [BsonIgnore]
        public string JobId => $"{Id?.ToString() ?? string.Empty}-{FacilityId ?? string.Empty}-{Topic ?? string.Empty}";
    }}