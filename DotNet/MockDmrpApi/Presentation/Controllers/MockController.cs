using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedSortOrder = LantanaGroup.Link.Shared.Application.Enums.SortOrder;

namespace LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;

/// <summary>
/// Support surface: seeds and inspects the data the contract endpoints serve.
/// </summary>
/// <remarks>
/// These endpoints are <b>ours</b>, not the third party's. They exist so a test can set up an
/// exact reporting plan scenario, and they are deliberately absent from
/// Contracts/dmrp-openapi.yaml so that replacing that document with the published contract
/// cannot disturb them.
/// <para>
/// Hand-written rather than generated for the same reason, which also frees it from the
/// override hazards a generated base carries -- uninherited defaults and non-nullable
/// optional parameters.
/// </para>
/// <para>
/// Guarded by Link's standard authentication, like every other Link service. That is a
/// different system from the one guarding <see cref="DmrpController"/>: these endpoints
/// belong to Link, the contract endpoints impersonate a third party and take that third
/// party's token. The token endpoint below issues the latter, which is why it lives here but
/// hands out something the contract endpoints accept.
/// </para>
/// </remarks>
[ApiController]
[Route("mock")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[Produces("application/json")]
public class MockController : ControllerBase
{
    private const string InvalidIdFormat = "Invalid Id format";

    private readonly IReportingPlanService _reportingPlans;
    private readonly IAuthTokenService _tokens;

    public MockController(IReportingPlanService reportingPlans, IAuthTokenService tokens)
    {
        _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    // ------------------------------------------------------------------ reads

    /// <summary>Gets one entry by identifier.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MockEntryModel>> GetById(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return Problem(detail: InvalidIdFormat, statusCode: StatusCodes.Status400BadRequest);
        }

        var entry = await _reportingPlans.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        return Ok(MockEntryMapper.ToModel(entry));
    }

    /// <summary>Gets a facility's entries, paged. Answers 204 when it has none.</summary>
    [HttpGet("facilities/{facilityId}")]
    [ProducesResponseType(typeof(MockEntryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MockEntryPage>> GetByFacility(
        string facilityId,
        [FromQuery] int? pageSize = 10,
        [FromQuery] int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return Problem(detail: "facilityId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var (records, metadata) = await _reportingPlans.GetByFacilityAsync(
            sanitizedFacilityId, pageSize ?? 10, pageNumber ?? 1, cancellationToken);

        if (records.Count == 0)
        {
            return NoContent();
        }

        return Ok(MockEntryMapper.ToPage(records, metadata));
    }

    /// <summary>Searches entries. Every filter is optional; answers 204 when nothing matches.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(MockEntryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MockEntryPage>> Search(
        [FromQuery] string? facilityId = null,
        [FromQuery] string? component = null,
        [FromQuery] string? measure = null,
        [FromQuery] int? reportingMonth = null,
        [FromQuery] int? reportingYear = null,
        [FromQuery] string? isReporting = null,
        [FromQuery] ReportingPlanSortBy sortBy = ReportingPlanSortBy.CreateDate,
        [FromQuery] SharedSortOrder sortOrder = SharedSortOrder.Descending,
        [FromQuery] int? pageSize = 10,
        [FromQuery] int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var criteria = new ReportingPlanSearchCriteria
        {
            FacilityId = Blank(facilityId),
            Component = Blank(component),
            Measure = Blank(measure),
            ReportingMonth = reportingMonth,
            ReportingYear = reportingYear,
            IsReporting = Blank(isReporting),
            SortBy = sortBy,
            SortOrder = sortOrder,
            PageSize = pageSize ?? ReportingPlanSearchCriteria.DefaultPageSize,
            PageNumber = pageNumber ?? 1
        };

        var (records, metadata) = await _reportingPlans.SearchAsync(criteria, cancellationToken);

        if (records.Count == 0)
        {
            return NoContent();
        }

        return Ok(MockEntryMapper.ToPage(records, metadata));
    }

    // ----------------------------------------------------------------- writes

    /// <summary>Creates an entry.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MockEntryModel>> Create(
        [FromBody] MockEntryRequest body, CancellationToken cancellationToken = default)
    {
        var entity = MockEntryMapper.ToEntity(body);
        entity.FacilityId = entity.FacilityId.SanitizeAndRemove();
        entity.Component = entity.Component.SanitizeAndRemove();
        entity.Measure = entity.Measure.SanitizeAndRemove();

        try
        {
            var created = await _reportingPlans.CreateAsync(entity, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MockEntryMapper.ToModel(created));
        }
        catch (InvalidReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>Updates an existing entry. Never creates one.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MockEntryModel>> Update(
        string id, [FromBody] MockEntryUpdateRequest body, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return Problem(detail: InvalidIdFormat, statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(body.Id) || !string.Equals(body.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                detail: "The id in the request body must match the id in the route.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var entity = MockEntryMapper.ToEntity(body);
        entity.FacilityId = entity.FacilityId.SanitizeAndRemove();
        entity.Component = entity.Component.SanitizeAndRemove();
        entity.Measure = entity.Measure.SanitizeAndRemove();

        try
        {
            var updated = await _reportingPlans.UpdateAsync(entity, cancellationToken);
            if (updated is null)
            {
                // Update-only. Creating here would let a caller create entries through a
                // verb the contract says never creates.
                return NotFound();
            }

            return Accepted(MockEntryMapper.ToModel(updated));
        }
        catch (InvalidReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    // ---------------------------------------------------------------- deletes

    /// <summary>Deletes one entry.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return Problem(detail: InvalidIdFormat, statusCode: StatusCodes.Status400BadRequest);
        }

        var deleted = await _reportingPlans.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Deletes a facility's entries. Idempotent.</summary>
    [HttpDelete("facilities/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteByFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return Problem(detail: "facilityId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        await _reportingPlans.DeleteByFacilityAsync(sanitizedFacilityId, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes every entry.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken = default)
    {
        await _reportingPlans.DeleteAllAsync(cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------- auth

    /// <summary>
    /// Issues the token the contract endpoints accept, standing in for the reporting
    /// system's authorization server.
    /// </summary>
    /// <remarks>
    /// Reaching this endpoint needs a Link token; what it hands back is a third-party one.
    /// The two are unrelated, which is the point -- a caller exercises the same
    /// acquire-then-use sequence it will perform against the real service.
    /// </remarks>
    [HttpPost("oauth2/token")]
    [ProducesResponseType(typeof(MockTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MockTokenErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MockTokenErrorResponse), StatusCodes.Status401Unauthorized)]
    public ActionResult<MockTokenResponse> IssueToken([FromBody] MockTokenRequest body)
    {
        var result = _tokens.Issue(body.Grant_type, body.Client_id, body.Client_secret, body.Scope);

        if (!result.Succeeded)
        {
            // OAuth 2.0 error shape rather than problem details: this stands in for an
            // authorization server, and callers parse the documented error codes.
            var status = result.Error == AuthTokenError.InvalidClient
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;

            return StatusCode(status, new MockTokenErrorResponse
            {
                Error = ToErrorCode(result.Error!.Value),
                Error_description = result.ErrorDescription
            });
        }

        return Ok(new MockTokenResponse
        {
            Access_token = result.AccessToken!,
            Token_type = "Bearer",
            Expires_in = result.ExpiresInSeconds,
            Scope = result.Scope,
            Issued_at = result.IssuedAt
        });
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Sanitizes a filter, treating blank as "not supplied".</summary>
    private static string? Blank(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = value.SanitizeAndRemove();
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private static string ToErrorCode(AuthTokenError error) => error switch
    {
        AuthTokenError.InvalidClient => "invalid_client",
        AuthTokenError.UnsupportedGrantType => "unsupported_grant_type",
        _ => "invalid_request"
    };
}
