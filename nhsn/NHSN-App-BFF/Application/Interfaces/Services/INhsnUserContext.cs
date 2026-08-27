using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

/// <summary>
/// The authenticated caller, resolved from the validated gateway JWT on every request.
/// </summary>
/// <remarks>
/// The only sanctioned source of facility and role — never the route, query, body, header, or a
/// database row. A cached claim agrees with the token at write time and silently diverges later.
/// </remarks>
public interface INhsnUserContext
{
    /// <summary>Stable identifier for the caller, used for attribution.</summary>
    string ExternalUserId { get; }

    string Email { get; }

    string Name { get; }

    /// <summary>Group names from the token, de-duplicated. Never persisted.</summary>
    IReadOnlyList<string> Groups { get; }

    /// <summary>The <c>facility</c> claim, or null when the token carries none.</summary>
    string? FacilityId { get; }

    bool HasFacility { get; }

    bool IsFacilityAdmin { get; }

    NhsnAccessState AccessState { get; }

    /// <summary>
    /// The facility for the current request, or throws when the token carries no facility claim.
    /// Use from any operation that writes facility-scoped state, so a missing claim fails loudly
    /// rather than defaulting.
    /// </summary>
    string RequireFacilityId();
}
