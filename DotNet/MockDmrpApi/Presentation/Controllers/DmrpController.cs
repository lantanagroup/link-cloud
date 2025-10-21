using System.Globalization;
using LantanaGroup.Link.MockDmrpApi.Application.Extensions;
using LantanaGroup.Link.MockDmrpApi.Application.Mapping;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;

/// <summary>
/// Stands in for the third-party DMRP API described by Contracts/dmrp-openapi.yaml.
/// </summary>
/// <remarks>
/// These are the only two operations the real service is understood to expose. Everything
/// else this application serves is our own support surface and lives on
/// <see cref="MockController"/>, deliberately outside the contract.
/// <para>
/// <b>Both operations are placeholders.</b> The query parameters match LCG's record of the
/// real ones, but the response body does not and is expected to change once the published
/// contract arrives. They are kept deliberately thin for that reason -- validate the caller,
/// narrow by component and whatever filters were supplied, project -- so that a change to the
/// contract is absorbed by regenerating and fixing compile errors rather than by rewriting
/// behaviour.
/// </para>
/// <para>
/// The routes come from the contract and are not prefixed: this controller impersonates an
/// external service, so its paths are the paths that service is expected to publish.
/// </para>
/// <para>
/// Authentication here is the <em>third party's</em>, not Link's. The bearer token is one
/// issued by <c>POST /api/mock-dmrp/oauth2/token</c>, which stands in for the reporting system's
/// authorization server. Link's own authentication guards the support surface instead.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public class DmrpController : DmrpControllerBase
{
    private readonly IReportingPlanService _reportingPlans;
    private readonly IAuthTokenService _tokens;

    public DmrpController(IReportingPlanService reportingPlans, IAuthTokenService tokens)
    {
        _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    /// <summary>Monthly medicine reporting plan. Placeholder shape.</summary>
    /// <remarks>
    /// The optional parameters are restated as <c>string?</c>. NSwag emits them as plain
    /// <c>string</c>, and under an enabled nullable context <c>[ApiController]</c> treats a
    /// non-nullable reference parameter as required -- so leaving them as generated would make
    /// every filter the contract documents as optional mandatory in practice, and omitting one
    /// a 400.
    /// </remarks>
    public override async Task<ActionResult<ReportingPlanResponse>> GetMonthlyMedicineReportingPlan(
        string nhsnorgid,
        string? name,
        string? year,
        string? month,
        CancellationToken cancellationToken = default)
    {
        return await GetPlanAsync(
            ReportingComponents.Msc, nhsnorgid, name, year, month, cancellationToken);
    }

    /// <summary>Patient-safety reporting plan. Placeholder shape.</summary>
    /// <remarks>
    /// Reported monthly, like the medicine plan, and narrowed by <paramref name="month"/> the
    /// same way. The "annual" in this operation's path is part of its name rather than a
    /// statement about its cadence.
    /// </remarks>
    public override async Task<ActionResult<ReportingPlanResponse>> GetPatientSafetyAnnualReportingPlan(
        string nhsnorgid,
        string? name,
        string? year,
        string? month,
        CancellationToken cancellationToken = default)
    {
        return await GetPlanAsync(
            ReportingComponents.Ps, nhsnorgid, name, year, month, cancellationToken);
    }

    private async Task<ActionResult<ReportingPlanResponse>> GetPlanAsync(
        string component,
        string nhsnOrgId,
        string? name,
        string? year,
        string? month,
        CancellationToken cancellationToken)
    {
        if (!_tokens.TryValidate(Request.Headers.Authorization.ToString(), out _))
        {
            // Problem details rather than a bare Unauthorized(), so a caller gets a traceId
            // and a reason. Deliberately vague about which check failed -- missing, malformed
            // and expired are one answer, because distinguishing them helps an attacker more
            // than a caller.
            return Problem(
                detail: "A valid bearer token issued by the reporting system's authorization "
                        + "server is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                type: DmrpProblemTypes.Unauthorized);
        }

        if (!TryParsePeriod(year, nameof(year), null, null, out var reportingYear, out var invalidYear))
        {
            return invalidYear!;
        }

        if (!TryParsePeriod(month, nameof(month), 1, 12, out var reportingMonth, out var invalidMonth))
        {
            return invalidMonth!;
        }

        var facility = nhsnOrgId.SanitizeAndRemove();
        var measure = string.IsNullOrWhiteSpace(name) ? null : name.SanitizeAndRemove();

        var entries = await _reportingPlans.GetReportingPlanAsync(
            component, facility, measure, reportingMonth, reportingYear, cancellationToken);

        return Ok(EntryMapper.ToReportingPlan(
            facility, reportingMonth, reportingYear, entries, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Parses an optional period component, which the contract types as a string.
    /// </summary>
    /// <remarks>
    /// Absent is valid and means "do not narrow by this". Present but not an integer is a
    /// malformed request rather than a filter that matches nothing: answering 200 with an
    /// empty plan would let a typo read as "enrolled in nothing", which is exactly the
    /// conclusion this API exists to convey and the one it must not convey by accident.
    /// <para>
    /// The real API's behaviour here is unknown. This is a guess, and a deliberately loud one.
    /// </para>
    /// </remarks>
    private ActionResult<ReportingPlanResponse>? ParseFailure(string parameterName, string value, int? min, int? max)
    {
        var expected = min is null || max is null
            ? "a whole number"
            : $"a whole number between {min} and {max}";

        return Problem(
            detail: $"'{parameterName}' must be {expected}, or be omitted. Received '{value.SanitizeAndRemove()}'.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid Reporting Period",
            type: DmrpProblemTypes.BadRequest);
    }

    private bool TryParsePeriod(
        string? raw,
        string parameterName,
        int? min,
        int? max,
        out int? value,
        out ActionResult<ReportingPlanResponse>? failure)
    {
        value = null;
        failure = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            failure = ParseFailure(parameterName, raw, min, max);
            return false;
        }

        if ((min is not null && parsed < min) || (max is not null && parsed > max))
        {
            failure = ParseFailure(parameterName, raw, min, max);
            return false;
        }

        value = parsed;
        return true;
    }
}
