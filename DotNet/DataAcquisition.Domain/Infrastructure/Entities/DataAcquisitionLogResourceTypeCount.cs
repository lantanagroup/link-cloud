using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// How many resources of one type a data acquisition log acquired, stored when the log's resource ids are saved.
/// </summary>
[Table("DataAcquisitionLogResourceTypeCounts")]
public class DataAcquisitionLogResourceTypeCount
{
    /// <summary>
    /// The row's identity.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// The log whose acquired resource ids were counted.
    /// </summary>
    public long DataAcquisitionLogId { get; set; }

    /// <summary>
    /// The log whose acquired resource ids were counted.
    /// </summary>
    [ForeignKey(nameof(DataAcquisitionLogId))]
    public virtual DataAcquisitionLog DataAcquisitionLog { get; set; } = null!;

    /// <summary>
    /// The text before the first '/' of the resource ids counted, or the whole id when it has none.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// How many of the log's resource ids have this resource type.
    /// </summary>
    public int Count { get; set; }
}
