namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// The stored value behind one of the report detail mapping indicators — Location Org, Encounter Mapping,
/// or HSLOC Mapping.
/// </summary>
/// <remarks>
/// <para>
/// All three columns share this type so the client has one rendering path rather than three. It reconciles
/// what the upstream services report: the code map statuses map across by name, while the Location Org and
/// Encounter Mapping values are resolved from the encounter counts the acquisition outcome carries.
/// </para>
/// <para>
/// The values are persisted as integers, so their ordinals are part of the stored data. New members must be
/// appended rather than inserted, or every existing row silently changes meaning.
/// </para>
/// </remarks>
public enum MappingIndicatorStatus
{
    /// <summary>
    /// Nothing has been recorded for this entry yet, because no outcome message has arrived for its source.
    /// </summary>
    /// <remarks>
    /// Zero is explicit because it is the column default: a row inserted by one source starts with the other
    /// source's columns in this state. Distinct from <see cref="NotApplicable"/> — this says the question has
    /// not been answered, not that it does not apply. A report that has closed with entries still in this
    /// state lost a message rather than having nothing to say.
    /// </remarks>
    NotEvaluated = 0,

    /// <summary>
    /// The question does not apply to this patient: the facility has not configured the mapping, or the
    /// patient had nothing for it to act on.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Everything that could be mapped was mapped.
    /// </summary>
    Mapped,

    /// <summary>
    /// Some mapped and some did not. The detail records which, since the column alone cannot say how much of
    /// the patient's data is affected.
    /// </summary>
    PartiallyMapped,

    /// <summary>
    /// Nothing mapped, though there was something to map. This is a real negative result rather than an
    /// absence of one.
    /// </summary>
    Unmapped,

    /// <summary>
    /// The mapping could not be determined because the operation itself failed.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Unmapped"/> so a processing fault is not reported as a gap in the
    /// facility's configuration, and apart from <see cref="Mapped"/> so it is not hidden entirely.
    /// </remarks>
    Unknown,

    /// <summary>
    /// The patient was treated as belonging to the reporting organization by default, because their
    /// encounters carried no resolvable location references — so membership was never actually verified
    /// against the facility's configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Produced only for the Location Org indicator, and only here: the acquisition contract reports the
    /// counts this is derived from rather than the distinction itself.
    /// </para>
    /// <para>
    /// A strict refinement of <see cref="Mapped"/> — a client that does not recognize this member can treat
    /// it as mapped without being wrong. It is last so that adding it shifted no stored ordinal.
    /// </para>
    /// </remarks>
    Assumed,

    /// <summary>
    /// The mapping is configured but nothing reached it to be mapped -- no resource of the relevant type
    /// was acquired for this patient. Distinct from <see cref="NotApplicable"/>, which means nothing is
    /// configured to produce the value at all.
    /// </summary>
    /// <remarks>
    /// The two are separated because they call for different action: this one points at the acquisition
    /// side (a query plan that fetched nothing), <see cref="NotApplicable"/> at the facility's
    /// configuration. Reporting both as NotApplicable told an operator to go and check a code map that was
    /// already correct.
    /// </remarks>
    NothingToEvaluate
}
