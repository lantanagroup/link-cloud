using LantanaGroup.Link.MockDmrpApi.Application.Mapping;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Mvc;
using SharedSortOrder = LantanaGroup.Link.Shared.Application.Enums.SortOrder;

namespace LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;

/// <summary>
/// Implements the DMRP contract described by Contracts/dmrp-openapi.yaml.
/// </summary>
/// <remarks>
/// The base class, its routes and its DTOs are generated from that document, so replacing
/// the document surfaces every contract change as a compile error here.
/// <para>
/// The contract is rooted at <c>/</c> because it describes the API as it is expected to be
/// published. This service stands in for that API, so it hosts the whole contract under a
/// prefix instead.
/// </para>
/// <para>
/// Two things must be restated on every override, neither of which is inherited from the
/// generated base -- see <c>GeneratedControllerBindingTests</c>:
/// default parameter values, and the nullability of optional string parameters. Dropping a
/// default silently unpages an endpoint; leaving an optional filter non-nullable makes
/// <c>[ApiController]</c> reject requests the contract allows.
/// </para>
/// </remarks>
[ApiController]
[Route("dmrp/mock")]
[Produces("application/json")]
public class DmrpController : DmrpControllerBase
{
    private const string InvalidIdFormat = "Invalid Id format";

    private readonly IReportingPlanService _reportingPlans;
    private readonly IAuthTokenService _tokens;

    public DmrpController(IReportingPlanService reportingPlans, IAuthTokenService tokens)
    {
        _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    // ------------------------------------------------------------------ reads

    public override async Task<ActionResult<ReportingPlanEntry>> GetReportingPlanEntry(
        string id, CancellationToken cancellationToken = default)
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

        return Ok(EntryMapper.ToContract(entry));
    }

    public override async Task<ActionResult<ReportingPlanEntryPage>> GetReportingPlanEntriesByFacility(
        string facilityId,
        int? pageSize = 10,
        int? pageNumber = 1,
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

        return Ok(EntryMapper.ToPage(records, metadata));
    }

    public override async Task<ActionResult<ReportingPlanEntryPage>> SearchReportingPlanEntries(
        string? facilityId,
        string? measure,
        int? reportingMonth,
        int? reportingYear,
        string? isReporting,
        SortBy? sortBy = SortBy.CreateDate,
        SortOrder? sortOrder = SortOrder.Descending,
        int? pageSize = 10,
        int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var criteria = new ReportingPlanSearchCriteria
        {
            FacilityId = Blank(facilityId),
            Measure = Blank(measure),
            ReportingMonth = reportingMonth,
            ReportingYear = reportingYear,
            IsReporting = Blank(isReporting),
            SortBy = ToDomain(sortBy),
            SortOrder = sortOrder == SortOrder.Ascending ? SharedSortOrder.Ascending : SharedSortOrder.Descending,
            PageSize = pageSize ?? ReportingPlanSearchCriteria.DefaultPageSize,
            PageNumber = pageNumber ?? 1
        };

        var (records, metadata) = await _reportingPlans.SearchAsync(criteria, cancellationToken);

        if (records.Count == 0)
        {
            return NoContent();
        }

        return Ok(EntryMapper.ToPage(records, metadata));
    }

    public override async Task<ActionResult<ReportingPlanResponse>> GetReportingPlan(
        string facilityId, int reportingMonth, int reportingYear, CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryValidate(Request.Headers.Authorization.ToString(), out _))
        {
            return Problem(
                detail: "A valid bearer token is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return Problem(detail: "facilityId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var entries = await _reportingPlans.GetReportingPlanAsync(
            sanitizedFacilityId, reportingMonth, reportingYear, cancellationToken);

        // A facility enrolled in nothing is a meaningful answer, not an absent resource,
        // so this deliberately returns 200 with an empty collection rather than 204 or 404.
        return Ok(EntryMapper.ToReportingPlan(
            sanitizedFacilityId, reportingMonth, reportingYear, entries, DateTimeOffset.UtcNow));
    }

    // ----------------------------------------------------------------- writes

    public override async Task<ActionResult<ReportingPlanEntry>> CreateReportingPlanEntry(
        ReportingPlanEntryRequest body, CancellationToken cancellationToken = default)
    {
        var entity = EntryMapper.ToEntity(body);
        entity.FacilityId = entity.FacilityId.SanitizeAndRemove();
        entity.Measure = entity.Measure.SanitizeAndRemove();

        try
        {
            var created = await _reportingPlans.CreateAsync(entity, cancellationToken);
            var contract = EntryMapper.ToContract(created);

            return CreatedAtAction(nameof(GetReportingPlanEntry), new { id = created.Id }, contract);
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    public override async Task<ActionResult<ReportingPlanEntry>> UpdateReportingPlanEntry(
        string id, ReportingPlanEntry body, CancellationToken cancellationToken = default)
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

        var entity = EntryMapper.ToEntity(body);
        entity.FacilityId = entity.FacilityId.SanitizeAndRemove();
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

            return Accepted(EntryMapper.ToContract(updated));
        }
        catch (DuplicateReportingPlanEntryException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    // ---------------------------------------------------------------- deletes

    public override async Task<IActionResult> DeleteReportingPlanEntry(
        string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out _))
        {
            return Problem(detail: InvalidIdFormat, statusCode: StatusCodes.Status400BadRequest);
        }

        var deleted = await _reportingPlans.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    public override async Task<IActionResult> DeleteReportingPlanEntriesByFacility(
        string facilityId, CancellationToken cancellationToken = default)
    {
        var sanitizedFacilityId = facilityId.SanitizeAndRemove();
        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return Problem(detail: "facilityId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Idempotent: succeeds whether or not the facility had entries.
        await _reportingPlans.DeleteByFacilityAsync(sanitizedFacilityId, cancellationToken);
        return NoContent();
    }

    public override async Task<IActionResult> DeleteAllReportingPlanEntries(CancellationToken cancellationToken = default)
    {
        await _reportingPlans.DeleteAllAsync(cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------- auth

    public override Task<ActionResult<AuthTokenResponse>> IssueToken(
        TokenRequest body, CancellationToken cancellationToken = default)
    {
        // The contract types grant_type as an enum, so its ToString is the C# member name
        // ("Client_credentials") rather than the wire value. Map it back explicitly.
        //
        // A consequence worth knowing: because the enum admits only one value, an unknown
        // grant is rejected by model binding as a validation 400 and never reaches the
        // service's unsupported_grant_type branch. A real authorization server would
        // answer that case with an OAuth error body instead.
        var grantType = body.Grant_type == TokenRequestGrant_type.Client_credentials
            ? "client_credentials"
            : body.Grant_type.ToString();

        var result = _tokens.Issue(grantType, body.Client_id, body.Client_secret, body.Scope);

        if (!result.Succeeded)
        {
            // OAuth 2.0 error shape rather than problem details: this operation stands in
            // for an authorization server, and callers parse the documented error codes.
            var status = result.Error == AuthTokenError.InvalidClient
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;

            return Task.FromResult<ActionResult<AuthTokenResponse>>(StatusCode(status, new AuthErrorResponse
            {
                Error = ToContractError(result.Error!.Value),
                Error_description = result.ErrorDescription
            }));
        }

        return Task.FromResult<ActionResult<AuthTokenResponse>>(Ok(new AuthTokenResponse
        {
            Access_token = result.AccessToken,
            Token_type = AuthTokenResponseToken_type.Bearer,
            Expires_in = result.ExpiresInSeconds,
            Scope = result.Scope,
            Issued_at = result.IssuedAt
        }));
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

    private static ReportingPlanSortBy ToDomain(SortBy? sortBy) => sortBy switch
    {
        SortBy.FacilityId => ReportingPlanSortBy.FacilityId,
        SortBy.Measure => ReportingPlanSortBy.Measure,
        SortBy.ReportingMonth => ReportingPlanSortBy.ReportingMonth,
        SortBy.ReportingYear => ReportingPlanSortBy.ReportingYear,
        SortBy.ModifyDate => ReportingPlanSortBy.ModifyDate,
        _ => ReportingPlanSortBy.CreateDate
    };

    private static AuthErrorResponseError ToContractError(AuthTokenError error) => error switch
    {
        AuthTokenError.InvalidClient => AuthErrorResponseError.Invalid_client,
        AuthTokenError.UnsupportedGrantType => AuthErrorResponseError.Unsupported_grant_type,
        _ => AuthErrorResponseError.Invalid_request
    };
}
