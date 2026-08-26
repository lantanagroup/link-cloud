namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// The result of applying one code map to a patient's resources, projected from the mapped, unmapped and
/// failure counts on <see cref="CodeMapOutcome"/>.
/// </summary>
public enum MappingStatus
{
    /// <summary>
    /// The code map ran but had nothing to act on — no coding in the patient's resources used its source
    /// system. Not a failure and not a configuration gap.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Every coding that used the map's source system was rewritten to its target system.
    /// </summary>
    Mapped,

    /// <summary>
    /// Some codings were rewritten and some had no entry in the map. The unmapped codes are the ones to
    /// add to the facility's configuration.
    /// </summary>
    PartiallyMapped,

    /// <summary>
    /// Codings used the map's source system but none had an entry in the map, so none were rewritten.
    /// </summary>
    Unmapped,

    /// <summary>
    /// The code map operation failed, so nothing can be said either way. Kept distinct from
    /// <see cref="Unmapped"/> so a processing fault is not reported as a gap in the facility's
    /// configuration.
    /// </summary>
    Unknown
}
