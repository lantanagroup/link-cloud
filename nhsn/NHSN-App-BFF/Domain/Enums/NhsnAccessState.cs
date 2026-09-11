namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

/// <summary>
/// Whether the authenticated principal may use NHSNLink, and if not, why.
/// Derived from the validated token on every request — never from persisted state.
/// </summary>
/// <remarks>
/// The member names are the wire values of <c>UserInfoResponse.AccessState</c>. The NHSN App
/// component branches on those strings, so renaming a member is a breaking API change.
/// </remarks>
public enum NhsnAccessState
{
    /// <summary>No <c>facility</c> claim on the token.</summary>
    MissingFacility,

    /// <summary>A facility is present but the token does not carry the FACADMIN group.</summary>
    MissingRequiredRole,

    /// <summary>Facility and FACADMIN both present.</summary>
    Allowed
}
