#nullable disable
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[Table("NormalizationConfig")]
public partial class NormalizationConfig
{
    [Key]
    public int Id { get; set; }

    [Unicode(false)]
    public string FacilityId { get; set; }

    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }
}