using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

[BsonKnownTypes(typeof(CopyLocationIdentifierToTypeOperation), typeof(CopyElementOperation), typeof(ConceptMapOperation), typeof(ConditionalTransformationOperation))]
[JsonDerivedType(typeof(ConceptMapOperation), nameof(ConceptMapOperation))]
[JsonDerivedType(typeof(CopyElementOperation), nameof(CopyElementOperation))]
[JsonDerivedType(typeof(CopyLocationIdentifierToTypeOperation), nameof(CopyLocationIdentifierToTypeOperation))]
[JsonDerivedType(typeof(ConditionalTransformationOperation), nameof(ConditionalTransformationOperation))]
public abstract class INormalizationOperation { }
