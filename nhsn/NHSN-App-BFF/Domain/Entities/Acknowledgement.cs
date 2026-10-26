using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

// An attestation record. Append-only — never updated in place, so a row's meaning never changes
// after it's written. Reading "the current acknowledgement" for a facility and Kind means the
// most recent row, not the only row.
[Table("Acknowledgements")]
public class Acknowledgement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    public AcknowledgementKind Kind { get; set; }

    // The report id for a ReportAccuracy acknowledgement. Null for CensusAccuracy, which is
    // facility-scoped rather than tied to one report.
    [MaxLength(64)]
    public string? ContextId { get; set; }

    public bool Accepted { get; set; }

    [MaxLength(128)]
    public string StatementKey { get; set; } = string.Empty;

    public DateTime AcceptedOn { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string AcceptedByExternalUserId { get; set; } = string.Empty;
}
