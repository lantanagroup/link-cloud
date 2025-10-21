namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public sealed class PreparedReferenceLogExecutionModel
{
    public bool SkipFetch { get; init; }
    public IReadOnlyList<string> ResourceIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
