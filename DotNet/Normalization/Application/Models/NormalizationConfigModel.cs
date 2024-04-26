using LantanaGroup.Link.Normalization.Domain.JsonObjects;

namespace LantanaGroup.Link.Normalization.Application.Models;

public class NormalizationConfigModel
{
    public string FacilityId { get; set; }
    public Dictionary<string, INormalizationOperation> OperationSequence { get; set; }
}
