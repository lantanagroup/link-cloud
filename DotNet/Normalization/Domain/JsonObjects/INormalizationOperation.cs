using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Domain.JsonObjects;

[JsonDerivedType(typeof(ConceptMapOperation), nameof(ConceptMapOperation))]
[JsonDerivedType(typeof(CopyElementOperation), nameof(CopyElementOperation))]
[JsonDerivedType(typeof(CopyLocationIdentifierToTypeOperation), nameof(CopyLocationIdentifierToTypeOperation))]
[JsonDerivedType(typeof(ConditionalTransformationOperation), nameof(ConditionalTransformationOperation))]
[JsonDerivedType(typeof(PeriodDateFixerOperation), nameof(PeriodDateFixerOperation))]
public abstract class INormalizationOperation { }
