#nullable disable
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[Table("NormalizationConfig")]
public class NormalizationConfig : BaseEntityExtended
{
    [Unicode(false)]
    public string FacilityId { get; set; }

    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
}