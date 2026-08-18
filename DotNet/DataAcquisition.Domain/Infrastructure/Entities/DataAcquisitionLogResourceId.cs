using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("DataAcquisitionLogResourceIds")]
public class DataAcquisitionLogResourceId
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long? DataAcquisitionLogId { get; set; }

    [ForeignKey(nameof(DataAcquisitionLogId))]
    public virtual DataAcquisitionLog DataAcquisitionLog { get; set; } = null!;

    [Required]
    [MaxLength(512)]
    public string ResourceId { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
