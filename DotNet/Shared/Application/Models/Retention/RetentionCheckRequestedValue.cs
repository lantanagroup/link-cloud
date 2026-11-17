using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// The value of a <c>RetentionCheckRequested</c> message: Report asking every participant to delete the
/// data it holds for one retention cleanup.
/// </summary>
/// <remarks>
/// Keyed by <see cref="RetentionCleanupId"/>, so every message for one cleanup lands on one partition. The
/// contract, including the rules participants follow, is in ADR 08; see dev-docs/retention-events.md.
/// </remarks>
public class RetentionCheckRequestedValue
{
    /// <summary>
    /// The cleanup's id, a UUID. It stays the same for the life of the cleanup, retries and re-sends
    /// included, so a participant must never skip a request because it has seen the id before.
    /// </summary>
    public required string RetentionCleanupId { get; set; }

    /// <summary>
    /// What started the cleanup.
    /// </summary>
    public required RetentionInitiatedBy InitiatedBy { get; set; }

    /// <summary>
    /// When Report created the cleanup, in UTC.
    /// </summary>
    public required DateTimeOffset InitiatedDateTime { get; set; }

    /// <summary>
    /// How much of each report's data goes. Always <see cref="RetentionMode.Scrub"/> unless
    /// <see cref="InitiatedBy"/> is <see cref="RetentionInitiatedBy.SingleReport"/>.
    /// </summary>
    public required RetentionMode Mode { get; set; }

    /// <summary>
    /// The cutoff for the system default window, in UTC. Used only by participants whose data is not
    /// tied to a report.
    /// </summary>
    public required DateTimeOffset CutoffDateTime { get; set; }

    /// <summary>
    /// The complete scope of the cleanup. Never empty.
    /// </summary>
    public required List<RetentionReport> Reports { get; set; }

    /// <summary>
    /// The participants being asked, by service name. Absent or empty means every expected participant;
    /// a retry or re-send names only the participants being asked again.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Participants { get; set; }
}
