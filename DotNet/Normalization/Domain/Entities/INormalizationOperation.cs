using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonKnownTypes(typeof(CopyLocationIdentifierToTypeOperation), typeof(CopyElementOperation), typeof(ConceptMapOperation), typeof(ConditionalTransformationOperation), typeof(PeriodDateFixerOperation))]
[JsonDerivedType(typeof(ConceptMapOperation), nameof(ConceptMapOperation))]
[JsonDerivedType(typeof(CopyElementOperation), nameof(CopyElementOperation))]
[JsonDerivedType(typeof(CopyLocationIdentifierToTypeOperation), nameof(CopyLocationIdentifierToTypeOperation))]
[JsonDerivedType(typeof(ConditionalTransformationOperation), nameof(ConditionalTransformationOperation))]
[JsonDerivedType(typeof(PeriodDateFixerOperation), nameof(PeriodDateFixerOperation))]
public abstract class INormalizationOperation { }
