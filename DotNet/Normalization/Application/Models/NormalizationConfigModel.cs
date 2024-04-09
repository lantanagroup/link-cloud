using LantanaGroup.Link.Normalization.Domain.Entities;

namespace LantanaGroup.Link.Normalization.Application.Models;

public class NormalizationConfigModel
{
    public string FacilityId { get; set; }
    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
}
