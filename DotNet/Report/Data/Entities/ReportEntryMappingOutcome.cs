#nullable disable
using LantanaGroup.Link.Report.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// What the pipeline's mapping steps produced for one patient in one report, recorded when they were
/// computed so the report keeps the answer it was built from.
/// </summary>
/// <remarks>
/// <para>
/// Written by two independent sources — DataAcquisition for the location and encounter columns,
/// Normalization for HSLOC — arriving in either order. Each owns a disjoint set of columns and writes only
/// those, so neither can overwrite the other's work regardless of which lands last.
/// </para>
/// <para>
/// Deliberately not keyed to <see cref="ReportEntry"/>: an outcome can arrive before the entry exists, and
/// a foreign key there would force the write to be retried until it did. The unique index on
/// (ReportScheduleId, PatientId) is what makes the upsert safe instead.
/// </para>
/// <para>
/// The relationship to <see cref="ReportSchedule"/> has no such problem — the schedule is always committed
/// before acquisition is requested for it — so it is a real cascading foreign key. A mapping outcome has no
/// meaning once the schedule it describes is gone.
/// </para>
/// </remarks>
[Index("ReportScheduleId", "PatientId", Name = "IX_ReportEntryMappingOutcomes_Schedule_Patient", IsUnique = true)]
[Index("FacilityId", "PatientId", Name = "IX_ReportEntryMappingOutcomes_Facility_Patient")]
public partial class ReportEntryMappingOutcome
{
    [Key]
    public Guid Id { get; set; }

    public DateTime CreateDate { get; set; }

    /// <summary>
    /// Last write from either source. Shared deliberately: last-write-wins on a timestamp is harmless, and
    /// the per-source evaluated-at columns carry the provenance that matters.
    /// </summary>
    public DateTime? ModifyDate { get; set; }

    [Required]
    [StringLength(100)]
    public string FacilityId { get; set; }

    public Guid ReportScheduleId { get; set; }

    /// <summary>
    /// The bare patient id, matching <see cref="ReportEntry.PatientId"/>. Any resource-type prefix is
    /// stripped before the row is written, or the join to the entry silently finds nothing.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string PatientId { get; set; }

    /// <summary>Sourced by DataAcquisition. Did the patient resolve to the reporting organization?</summary>
    public MappingIndicatorStatus LocationOrgStatus { get; set; }

    /// <summary>Sourced by DataAcquisition. Were the patient's encounters mapped to locations at all?</summary>
    public MappingIndicatorStatus EncounterMappingStatus { get; set; }

    /// <summary>
    /// Sourced by DataAcquisition. The counts and matched locations behind the two columns above, as JSON.
    /// Storage only — never returned raw on an API.
    /// </summary>
    /// <remarks>
    /// Unbounded by design. The match list grows with the distinct locations one patient touched, so any
    /// cap would be an arbitrary limit that truncates the detail exactly when a patient is most tangled.
    /// Declared with an explicit maximum rather than a provider type name, so the column resolves to
    /// <c>nvarchar(max)</c> on SQL Server and <c>TEXT</c> on SQLite instead of pinning either.
    /// </remarks>
    [StringLength(int.MaxValue)]
    public string AcquisitionDetails { get; set; }

    /// <summary>
    /// Sourced by DataAcquisition. When the acquisition side last reported. Null while
    /// <see cref="LocationOrgStatus"/> is <see cref="MappingIndicatorStatus.NotEvaluated"/>; together they
    /// distinguish a patient still in flight from one whose message was lost.
    /// </summary>
    public DateTime? AcquisitionEvaluatedAt { get; set; }

    /// <summary>Sourced by Normalization. Were the patient's location codes translated to HSLOC?</summary>
    public MappingIndicatorStatus HslocMappingStatus { get; set; }

    /// <summary>
    /// Sourced by Normalization. Per-code-map counts and unmapped codes, as JSON. Retains every target system
    /// reported, including ones no column recognizes, so a mistyped system is findable from the record.
    /// </summary>
    /// <remarks>
    /// Unbounded by design: one entry per code map the facility exercised, and that count is configuration
    /// rather than something this table can bound.
    /// </remarks>
    [StringLength(int.MaxValue)]
    public string NormalizationDetails { get; set; }

    /// <summary>Sourced by Normalization. When the normalization side last reported.</summary>
    public DateTime? NormalizationEvaluatedAt { get; set; }

    [ForeignKey("ReportScheduleId")]
    public virtual ReportSchedule ReportSchedule { get; set; }
}
