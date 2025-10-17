using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("fhirQueryConfiguration")]
public class FhirQueryConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FacilityId { get; set; }
    public string FhirServerBaseUrl { get; set; }
    [BsonIgnoreIfNull]
    public AuthenticationConfiguration? Authentication { get; set; }
    public int? MaxConcurrentRequests { get; set; } = 8;
    public TimeSpan? MinAcquisitionPullTime { get; set; }
    public TimeSpan? MaxAcquisitionPullTime { get; set; }
    public string? TimeZone { get; set; } = null;
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }

}
