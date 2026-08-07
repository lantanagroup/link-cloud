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
/// <b>Both operations are placeholders.</b> Their shape is expected to change once the
/// published contract arrives. They are kept deliberately thin for that reason -- validate
/// the caller, select by component, project -- so that a change to the contract is absorbed
/// by regenerating and fixing compile errors rather than by rewriting behaviour.
/// </para>
/// <para>
/// The routes come from the contract and are not prefixed: this controller impersonates an
/// external service, so its paths are the paths that service is expected to publish.
/// </para>
/// <para>
/// Authentication here is the <em>third party's</em>, not Link's. The bearer token is one
/// issued by <c>POST /mock/oauth2/token</c>, which stands in for the reporting system's
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
    public override async Task<ActionResult<ReportingPlanResponse>> GetMonthlyMedicineReportingPlan(
        string facilityId, int reportingMonth, int reportingYear, CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryValidate(Request.Headers.Authorization.ToString(), out _))
        {
            return Unauthorized();
        }

        var sanitizedFacilityId = facilityId.SanitizeAndRemove();

        var entries = await _reportingPlans.GetMonthlyReportingPlanAsync(
            ReportingComponents.Msc, sanitizedFacilityId, reportingMonth, reportingYear, cancellationToken);

        return Ok(EntryMapper.ToReportingPlan(
            sanitizedFacilityId, reportingMonth, reportingYear, entries, DateTimeOffset.UtcNow));
    }

    /// <summary>Annual patient-safety reporting plan. Placeholder shape.</summary>
    public override async Task<ActionResult<ReportingPlanResponse>> GetPatientSafetyAnnualReportingPlan(
        string facilityId, int reportingYear, CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryValidate(Request.Headers.Authorization.ToString(), out _))
        {
            return Unauthorized();
        }

        var sanitizedFacilityId = facilityId.SanitizeAndRemove();

        var entries = await _reportingPlans.GetAnnualReportingPlanAsync(
            ReportingComponents.Ps, sanitizedFacilityId, reportingYear, cancellationToken);

        // reportingMonth is null: this plan is annual, and the response omits it.
        return Ok(EntryMapper.ToReportingPlan(
            sanitizedFacilityId, reportingMonth: null, reportingYear, entries, DateTimeOffset.UtcNow));
    }
}
