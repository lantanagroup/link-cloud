namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// The value of a <c>MappingOutcomeEvaluated</c> message: what one patient's mapping steps produced
/// during one acquisition correlation.
/// </summary>
/// <remarks>
/// <para>
/// Two services produce this topic independently — DataAcquisition after it resolves org membership, and
/// Normalization after it applies the facility's code maps — so a patient yields two messages that may
/// arrive in either order. <see cref="Source"/> distinguishes them, and each producer reports only its
/// own results; neither reads or restates the other's.
/// </para>
/// <para>
/// It rides its own topic rather than enriching <c>ResourcesAcquired</c> or <c>ResourcesNormalized</c>
/// so that a failure to report an outcome cannot affect the pipeline that produces the report, and so
/// that a failure in one producer cannot discard an outcome the other already computed.
/// </para>
/// <para>
/// Keyed by <c>ResourceKey</c> (facility and patient), matching the other per-patient topics and
/// preserving per-facility ordering.
/// </para>
/// </remarks>
public class MappingOutcomeEvaluatedValue
{
    /// <summary>
    /// Which service produced this message. Determines which of the properties below carry meaning; see
    /// <see cref="MappingOutcomeSource"/>.
    /// </summary>
    public MappingOutcomeSource Source { get; set; }

    /// <summary>
    /// The report schedules this patient's correlation was acquired for, copied from the message that
    /// triggered the work. One outcome fans out to every schedule listed here, since a single
    /// acquisition can serve more than one open reporting period.
    /// </summary>
    public List<ScheduledReport> ScheduledReports { get; set; } = [];

    /// <summary>
    /// How the patient resolved against the facility's org-location configuration. Populated only when
    /// <see cref="Source"/> is <see cref="MappingOutcomeSource.Acquisition"/>; null otherwise, which
    /// carries no information.
    /// </summary>
    public LocationOrgOutcome? LocationOrgOutcome { get; set; }

    /// <summary>
    /// One entry per code map exercised against the patient's resources, labelled by the source and
    /// target systems it was configured with. Populated only when <see cref="Source"/> is
    /// <see cref="MappingOutcomeSource.Normalization"/>, where an empty list is itself a result — the
    /// facility configured no code maps — rather than an absence of one.
    /// <para>
    /// Every outcome is reported, including target systems no consumer recognizes; narrowing them to the
    /// indicators a report displays is the consumer's decision, not the producer's.
    /// </para>
    /// </summary>
    public List<CodeMapOutcome> CodeMapOutcomes { get; set; } = [];

    /// <summary>
    /// Identifies the acquisition pass these outcomes describe, so a consumer can tell a genuinely new
    /// pass from a redelivery of one it has already recorded.
    /// </summary>
    /// <remarks>
    /// Kafka is at-least-once and Report commits its offset after writing, so a crash between the two
    /// redelivers the message. A consumer that combines outcomes across passes -- which it must, because a
    /// reportable patient is acquired twice and each pass sees only its own resources -- would otherwise
    /// count the redelivered pass a second time. The correlation id alone is not enough: the initial and
    /// supplemental passes of one patient share it, and they are the two passes being combined.
    /// </remarks>
    public string? CorrelationId { get; set; }

    /// <inheritdoc cref="CorrelationId"/>
    public string? QueryType { get; set; }
}
