using Link.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers.Api;

/// <summary>
/// Readiness of the named EHR systems configured for this process.
/// </summary>
[ApiController]
[Route("api/ehr-systems")]
[Authorize(Policy = "ApiBearerPolicy")]
public sealed class EhrSystemsApiController(EhrSystemCatalog catalog, EhrSystemReadinessChecker readiness) : ControllerBase
{
    /// <summary>Probes every configured EHR system.</summary>
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(CancellationToken cancellationToken)
    {
        var records = new List<EhrReadinessResult>(catalog.Systems.Count);
        foreach (var system in catalog.Systems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(await readiness.CheckAsync(system, cancellationToken));
        }

        return Ok(new { records });
    }

    /// <summary>Probes the named EHR system.</summary>
    [HttpGet("{name}/readiness")]
    public async Task<IActionResult> ReadinessFor(string name, CancellationToken cancellationToken)
    {
        var system = catalog.Find(name);
        if (system is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "EHR system not found",
                detail: "That EHR system is not configured.");
        }

        return Ok(await readiness.CheckAsync(system, cancellationToken));
    }
}
