using LantanaGroup.Link.Normalization.Application.Services.ReportActivity;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers;

[Route("api/normalization/reports")]
[ApiController]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
public class ReportActivityController : ControllerBase
{
    private readonly INormalizationReportActivityStore _store;

    public ReportActivityController(INormalizationReportActivityStore store) => _store = store;

    /// <summary>Resources normalized for one report: a single aggregate over that report's rows.</summary>
    [HttpGet("{reportId}/summary")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NormalizationReportSummary))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NormalizationReportSummary>> Summary(string reportId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            return BadRequest("reportId is required.");
        return Ok(await _store.SummaryAsync(reportId, cancellationToken));
    }
}