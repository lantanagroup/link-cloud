namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// Which service produced a <see cref="MappingOutcomeValue"/>, and therefore which of its fields are
/// authoritative.
/// </summary>
/// <remarks>
/// Present so that an empty collection is unambiguous. Normalization legitimately reports no code map
/// outcomes when a facility has configured none, which is a real result; DataAcquisition reports none
/// because code maps are not its concern. Inspecting the fields cannot tell those apart, so the consumer
/// switches on this instead — and uses it to decide which stored values the message is allowed to write,
/// which is what keeps two producers for the same patient from overwriting each other.
/// </remarks>
public enum MappingOutcomeSource
{
    /// <summary>
    /// DataAcquisition. <see cref="MappingOutcomeValue.LocationOrgOutcome"/> is authoritative;
    /// <see cref="MappingOutcomeValue.CodeMapOutcomes"/> is always empty and means nothing.
    /// </summary>
    Acquisition,

    /// <summary>
    /// Normalization. <see cref="MappingOutcomeValue.CodeMapOutcomes"/> is authoritative, including when
    /// it is empty; <see cref="MappingOutcomeValue.LocationOrgOutcome"/> is always null and means
    /// nothing.
    /// </summary>
    Normalization
}
