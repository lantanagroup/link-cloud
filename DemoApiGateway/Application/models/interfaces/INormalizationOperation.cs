using LantanaGroup.Link.DemoApiGateway.Application.models.normalization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces
{
    [JsonDerivedType(typeof(ConceptMapOperation), nameof(ConceptMapOperation))]
    [JsonDerivedType(typeof(CopyElementOperation), nameof(CopyElementOperation))]
    [JsonDerivedType(typeof(CopyLocationIdentifierToTypeOperation), nameof(CopyLocationIdentifierToTypeOperation))]
    [JsonDerivedType(typeof(ConditionalTransformationOperation), nameof(ConditionalTransformationOperation))]
    public interface INormalizationOperation
    {
    }
}
