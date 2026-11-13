namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// Where one section of the assembled draft came from, and whether it arrived.
//
// Partial failure is reported per section, not per request. A step renders an error only when its
// own source failed, so a user on step 3 isn't blocked by a Data Acquisition outage.
//
// A section whose source is unreachable must render an error rather than an empty form — an empty
// form reads as "nothing configured" and invites the user to re-enter data that already exists.
public sealed record SectionSource
{
    public required string Section { get; init; }

    // Which system owns it — "Tenant", "DataAcquisition", "Census", "Bff".
    public required string Origin { get; init; }

    public required SectionStatus Status { get; init; }

    // The downstream trace id when the read failed.
    public string? TraceId { get; init; }

    // Why it failed, in terms a facility administrator can escalate with. Never a stack trace.
    public string? Detail { get; init; }
}

public enum SectionStatus
{
    Ok,

    // The owning service could not be read. Distinct from "read fine, nothing configured".
    Unavailable
}
