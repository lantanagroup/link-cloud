using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("fhirQueryConfiguration")]
public partial class FhirQueryConfiguration
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    [Required]
    public string FhirServerBaseUrl { get; set; }

    public AuthenticationConfiguration? Authentication { get; set; }

    public TimeSpan? MaxAcquisitionPullTime { get; set; }

    public int? MaxConcurrentRequests { get; set; }

    public TimeSpan? MinAcquisitionPullTime { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }
}
