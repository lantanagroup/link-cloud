using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;

/// <summary>
/// Stands in for the NHSN Auth API, which a caller authenticates against before querying
/// DMRP.
/// </summary>
/// <remarks>
/// Deliberately outside Contracts/dmrp-openapi.yaml. That document describes the DMRP API
/// as it is expected to be published, and authentication is a separate service rather than
/// a DMRP operation; "auth-test" is a name for exercising a stand-in, not part of anyone's
/// published contract.
/// <para>
/// It delegates to <see cref="DmrpController.IssueToken"/> so there is exactly one token
/// implementation. Because it sits outside the contract, codegen cannot catch it if the two
/// drift -- the tests assert the token it issues is accepted by the contract operation.
/// </para>
/// <para>
/// The client secret travels as a query parameter here, which is why this is a simulation
/// aid rather than the route to integrate against. Query strings reach access logs; point
/// real callers at <c>POST /dmrp/mock/oauth2/token</c>.
/// </para>
/// <para>
/// There is deliberately no matching route for the reporting plan query. That operation is
/// part of the real API and is already served at <c>GET /dmrp/mock/reporting-plans</c>;
/// a second door to it would only invite callers to integrate against a path the real
/// service does not have.
/// </para>
/// </remarks>
[ApiController]
[Route("dmrp/mock")]
[Produces("application/json")]
public class NhsnAuthController : ControllerBase
{
    private readonly DmrpController _dmrp;

    public NhsnAuthController(DmrpController dmrp)
    {
        _dmrp = dmrp ?? throw new ArgumentNullException(nameof(dmrp));
    }

    /// <summary>Simulates the auth workflow of the NHSN Auth API.</summary>
    [HttpGet("auth-test")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthErrorResponse), StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<AuthTokenResponse>> AuthTest(
        [FromQuery] string clientId,
        [FromQuery] string clientSecret,
        [FromQuery] string? scope = null,
        CancellationToken cancellationToken = default)
    {
        _dmrp.ControllerContext = ControllerContext;
        return _dmrp.IssueToken(
            new TokenRequest
            {
                Grant_type = TokenRequestGrant_type.Client_credentials,
                Client_id = clientId,
                Client_secret = clientSecret,
                Scope = scope!
            },
            cancellationToken);
    }
}
