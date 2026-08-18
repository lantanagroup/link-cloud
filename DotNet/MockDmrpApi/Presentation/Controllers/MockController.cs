using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.MockDmrpApi.Application.Extensions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
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
[Route("api/mock-dmrp")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[Produces("application/json")]
public class MockController : ControllerBase
{
    private const string InvalidIdFormat = "Invalid Id format";

    private readonly IReportingPlanService _reportingPlans;
    private readonly IAuthTokenService _tokens;
    private readonly IResponseDelayService _delays;

    public MockController(
        IReportingPlanService reportingPlans, IAuthTokenService tokens, IResponseDelayService delays)
    {
        _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _delays = delays ?? throw new ArgumentNullException(nameof(delays));
    }

    // ------------------------------------------------------------------ reads

    /// <summary>Gets one entry by identifier.</summary>
    [HttpGet("entries/{id}")]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MockEntryModel>> GetById(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return InvalidId();
        }

        var entry = await _reportingPlans.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return EntryNotFound(id);
        }

        return Ok(MockEntryMapper.ToModel(entry));
    }

    /// <summary>Gets a facility's entries, paged. Answers an empty page when it has none.</summary>
    /// <remarks>
    /// No 404: zero entries means the facility has no reporting plans, not that it does not
    /// exist. <c>ReportingPlanEntry</c> is the only table here, so that second claim cannot be
    /// checked. A blank identifier is still a 400.
    /// </remarks>
    [HttpGet("facilities/{facilityId}/entries")]
    [ProducesResponseType(typeof(MockEntryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MockEntryPage>> GetByFacility(
        string facilityId,
        [FromQuery] [Range(1, ReportingPlanSearchCriteria.MaxPageSize)] int? pageSize = 10,
        [FromQuery] [Range(1, int.MaxValue)] int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return MissingFacilityId();
        }

        var (records, metadata) = await _reportingPlans.GetByFacilityAsync(
            sanitizedFacilityId, pageSize ?? 10, pageNumber ?? 1, cancellationToken);

        return Ok(MockEntryMapper.ToPage(records, metadata));
    }

    /// <summary>
    /// Searches entries. Every filter is optional; answers an empty page when nothing matches.
    /// </summary>
    [HttpGet("entries/search")]
    [ProducesResponseType(typeof(MockEntryPage), StatusCodes.Status200OK)]
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
        [FromQuery] [Range(1, ReportingPlanSearchCriteria.MaxPageSize)] int? pageSize = 10,
        [FromQuery] [Range(1, int.MaxValue)] int? pageNumber = 1,
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

        return Ok(MockEntryMapper.ToPage(records, metadata));
    }

    // ----------------------------------------------------------------- writes

    /// <summary>Creates an entry.</summary>
    [HttpPost("entries")]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MockEntryModel>> Create(
        [FromBody] MockEntryRequest body, CancellationToken cancellationToken = default)
    {
        var entity = MockEntryMapper.ToEntity(body);
        Sanitize(entity);

        try
        {
            var created = await _reportingPlans.CreateAsync(entity, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MockEntryMapper.ToModel(created));
        }
        catch (InvalidReportingPlanEntryException ex)
        {
            return InvalidEntry(ex);
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return DuplicateEntry(ex);
        }
    }

    /// <summary>Updates an existing entry. Never creates one.</summary>
    [HttpPut("entries/{id}")]
    [ProducesResponseType(typeof(MockEntryModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MockEntryModel>> Update(
        string id, [FromBody] MockEntryUpdateRequest body, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return InvalidId();
        }

        if (string.IsNullOrWhiteSpace(body.Id) || !string.Equals(body.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                detail: "The id in the request body must match the id in the route.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Id Mismatch",
                type: DmrpProblemTypes.BadRequest);
        }

        var entity = MockEntryMapper.ToEntity(body);
        Sanitize(entity);

        try
        {
            var updated = await _reportingPlans.UpdateAsync(entity, cancellationToken);
            if (updated is null)
            {
                // Update-only. Creating here would let a caller create entries through a
                // verb the contract says never creates. The detail says so, because a bare
                // 404 from a PUT reads like a routing problem.
                return EntryNotFound(id, " This endpoint updates an existing entry and never creates one.");
            }

            return Accepted(MockEntryMapper.ToModel(updated));
        }
        catch (InvalidReportingPlanEntryException ex)
        {
            return InvalidEntry(ex);
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return DuplicateEntry(ex);
        }
    }

    // ---------------------------------------------------------------- deletes

    /// <summary>Deletes one entry.</summary>
    [HttpDelete("entries/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return InvalidId();
        }

        var deleted = await _reportingPlans.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : EntryNotFound(id);
    }

    /// <summary>Deletes a facility's entries. Idempotent.</summary>
    [HttpDelete("facilities/{facilityId}/entries")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteByFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return MissingFacilityId();
        }

        await _reportingPlans.DeleteByFacilityAsync(sanitizedFacilityId, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes every entry.</summary>
    [HttpDelete("entries")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken = default)
    {
        await _reportingPlans.DeleteAllAsync(cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------- response delay

    /// <summary>Gets the artificial delay currently applied to the contract endpoints.</summary>
    [HttpGet("delay")]
    [ProducesResponseType(typeof(MockDelayModel), StatusCodes.Status200OK)]
    public ActionResult<MockDelayModel> GetDelay()
    {
        return Ok(MockDelayModel.From(_delays.Current));
    }

    /// <summary>
    /// Sets an artificial delay on the contract endpoints, for exercising a caller's timeout
    /// and retry behaviour against a slow upstream.
    /// </summary>
    /// <remarks>
    /// Held in memory and never persisted, so a restart always clears it.
    /// <para>
    /// Answers 200 rather than the 202 this service uses for PUT elsewhere. 202 says the work
    /// has been accepted and will happen; this has already happened by the time the response
    /// is written, and the body is the state now in force.
    /// </para>
    /// <para>
    /// The delay never reaches <c>/api</c>, so this endpoint and its counterparts stay
    /// responsive no matter how long a delay is configured.
    /// </para>
    /// </remarks>
    [HttpPut("delay")]
    [ProducesResponseType(typeof(MockDelayModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<MockDelayModel> SetDelay([FromBody] MockDelayRequest body)
    {
        try
        {
            return Ok(MockDelayModel.From(_delays.Set(body.Milliseconds)));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // The annotation covers the ordinary case; this catches a bound value that got
            // past it, so the ceiling is enforced in one place rather than two.
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Delay",
                type: DmrpProblemTypes.BadRequest);
        }
    }

    /// <summary>Removes any artificial delay. Idempotent.</summary>
    [HttpDelete("delay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearDelay()
    {
        _delays.Clear();
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

    /// <summary>
    /// The problem responses this controller repeats, built in one place.
    /// </summary>
    /// <remarks>
    /// Each carries a title and a type as well as a detail. Passing only a detail leaves the
    /// framework's generic title beside a specific message, and gives a caller nothing to
    /// branch on but the status code -- which several of these share.
    /// </remarks>
    private ObjectResult InvalidId() => Problem(
        detail: InvalidIdFormat,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid Id",
        type: DmrpProblemTypes.BadRequest);

    private ObjectResult EntryNotFound(string id, string extra = "") => Problem(
        detail: $"No reporting plan entry was found with id '{id}'.{extra}",
        statusCode: StatusCodes.Status404NotFound,
        title: "Entry Not Found",
        type: DmrpProblemTypes.NotFound);

    private ObjectResult MissingFacilityId() => Problem(
        detail: "A 'facilityId' is required.",
        statusCode: StatusCodes.Status400BadRequest,
        title: "Missing Facility Id",
        type: DmrpProblemTypes.BadRequest);

    private ObjectResult InvalidEntry(InvalidReportingPlanEntryException ex) => Problem(
        detail: ex.Message,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid Reporting Plan Entry",
        type: DmrpProblemTypes.BadRequest);

    private ObjectResult DuplicateEntry(DuplicateReportingPlanEntryException ex) => Problem(
        detail: ex.Message,
        statusCode: StatusCodes.Status409Conflict,
        title: "Duplicate Reporting Plan Entry",
        type: DmrpProblemTypes.Conflict);

    /// <summary>
    /// Sanitizes every stored string on an entry, so a write stores what a read searches for.
    /// </summary>
    /// <remarks>
    /// <see cref="Blank"/> sanitizes the search filters, so any field sanitized on one side and
    /// not the other becomes unsearchable: a value that changes under sanitization would be
    /// stored in its original form and then compared against its sanitized form, which never
    /// matches. <c>IsReporting</c> carries a further cost, because
    /// <c>ReportingPlanService.GetReportingPlanAsync</c> selects on <c>IsReporting == "Y"</c> --
    /// a row stored as anything else silently drops out of the plan the contract endpoints
    /// serve.
    /// </remarks>
    private static void Sanitize(ReportingPlanEntryEntity entry)
    {
        entry.FacilityId = entry.FacilityId.SanitizeAndRemove();
        entry.Component = entry.Component.SanitizeAndRemove();
        entry.Measure = entry.Measure.SanitizeAndRemove();
        entry.IsReporting = entry.IsReporting.SanitizeAndRemove();
    }

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
