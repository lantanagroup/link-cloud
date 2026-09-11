using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;

namespace LantanaGroup.Link.Normalization.Engine;

public sealed record NormalizationWorkItem(int Sequence, IOperation Operation, IReadOnlyList<string> ResourceTypes);

public sealed record NormalizationStepOutcome(
    int Sequence,
    IOperation Operation,
    OperationResult Result);
