using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.MockDmrpApi.Application.Models;

/// <summary>
/// Token exchange models for the stand-in authorization server.
/// </summary>
/// <remarks>
/// Snake-cased on the wire because that is what OAuth 2.0 uses and what a client library
/// expects, even though it reads oddly as C#.
/// <para>
/// Hand-written alongside the rest of the support surface. The real reporting system's
/// authorization server is a separate service from DMRP, so its shape does not belong in the
/// DMRP contract either.
/// </para>
/// </remarks>
public class MockTokenRequest
{
    /// <summary>
    /// Only <c>client_credentials</c> is supported. Typed as a string rather than an enum so
    /// an unknown grant reaches the service and comes back as <c>unsupported_grant_type</c>,
    /// which is what an authorization server would answer -- an enum would make model
    /// binding reject it as a generic validation error instead.
    /// </summary>
    [Required]
    public string Grant_type { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Client_id { get; set; } = string.Empty;

    [Required]
    [StringLength(400)]
    public string Client_secret { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Scope { get; set; }
}

public class MockTokenResponse
{
    public string Access_token { get; set; } = string.Empty;
    public string Token_type { get; set; } = "Bearer";
    public int Expires_in { get; set; }
    public string? Scope { get; set; }
    public DateTimeOffset Issued_at { get; set; }
}

public class MockTokenErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Error_description { get; set; }
}
