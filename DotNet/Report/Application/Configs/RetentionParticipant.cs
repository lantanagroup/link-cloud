using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.Options;

/// <summary>
/// One service expected to reply to every retention cleanup, as validated from <c>Retention:Participants</c>.
/// </summary>
/// <param name="ServiceName">
/// The name the participant puts in a completion's <c>ServiceName</c>.
/// </param>
/// <param name="Scope">
/// Whether the participant replies per report or once for the whole cleanup.
/// </param>
public sealed record RetentionParticipant(string ServiceName, RetentionParticipantScope Scope);
