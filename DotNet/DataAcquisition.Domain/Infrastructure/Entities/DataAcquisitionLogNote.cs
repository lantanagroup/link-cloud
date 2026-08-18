using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("DataAcquisitionLogNotes")]
public class DataAcquisitionLogNote
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long? DataAcquisitionLogId { get; set; }

    [ForeignKey(nameof(DataAcquisitionLogId))]
    public virtual DataAcquisitionLog DataAcquisitionLog { get; set; } = null!;

    [Required]
    public string Note { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}