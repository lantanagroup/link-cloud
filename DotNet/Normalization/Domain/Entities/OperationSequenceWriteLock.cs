namespace LantanaGroup.Link.Normalization.Domain.Entities;

/// <summary>
/// One row per facility. Sequence creates and deletes lock this row before they
/// change sequences. It is not a cache revision, so a facility id cannot share
/// a key with its own revision row.
/// </summary>
public class OperationSequenceWriteLock
{
    public string FacilityId { get; set; } = "";
}
