namespace LantanaGroup.Link.Normalization.Domain.Entities;

/// <summary>
/// One row per facility. Writes that change cached operation sequences increment
/// <see cref="Revision"/> so every replica's next cached read misses, while other
/// facilities keep the entries they already hold.
/// </summary>
public class OperationSequenceCacheRevision
{
    public string FacilityId { get; set; } = "";

    public long Revision { get; set; }
}
