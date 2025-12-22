using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("SftpAcquisitionLog")]
public class SftpAcquisitionLog
{
    [Key]
    public long Id { get; set; }
    
    public Guid ExternalId { get; set; }
    
    public string FacilityId { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    
    public int PatientCount { get; set; }
    
    public int EncounterCount { get; set; }
    
    public DateTime ProcessDate { get; set; } = DateTime.UtcNow;
}