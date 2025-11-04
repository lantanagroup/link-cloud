using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("fhirListConfiguration")]
public class FhirListConfiguration
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    [Required]
    public string FhirBaseServerUrl { get; set; }

    public AuthenticationConfiguration? Authentication { get; set; }

    [Required]
    [Column("EHRPatientLists")]
    public List<EhrPatientList> EHRPatientLists { get; set; } = new List<EhrPatientList>();

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }
}
