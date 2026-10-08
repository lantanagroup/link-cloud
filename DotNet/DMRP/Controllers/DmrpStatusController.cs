using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.MockDmrp;
using LantanaGroup.Link.DMRP.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DMRP.Controllers;

/// <summary>
/// Tells the Admin UI whether DMRP is enabled and whether facility saves write through to the Mock DMRP API,
/// so the UI reads Tenant's own settings instead of keeping a copy of them.
/// </summary>
/// <remarks>
/// The one DMRP route served while the module is disabled, so a caller always gets an answer rather than
/// having to read a 404 as "off". Every other <c>api/dmrp</c> route still exists only while DMRP is enabled.
/// </remarks>
[Route("api/dmrp/dmrp-status")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class DmrpStatusController : ControllerBase
{
    private readonly IOptions<DmrpSettings> _settings;
    private readonly IMockDmrpStatus _mockStatus;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    public DmrpStatusController(IOptions<DmrpSettings> settings, IMockDmrpStatus mockStatus)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _mockStatus = mockStatus ?? throw new ArgumentNullException(nameof(mockStatus));
    }

    /// <summary>
    /// Gets whether DMRP is enabled and whether facility saves write through to the Mock DMRP API.
    /// </summary>
    /// <remarks>
    /// Reflects <c>DMRP:Enabled</c> and <c>MockDmrpApi:Enabled</c> as Tenant read them at startup, and never
    /// contacts DMRP or the mock. A change to either shows here after Tenant restarts.
    /// </remarks>
    /// <response code="200">Both flags. The mock flag is never true while DMRP is disabled.</response>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DmrpStatusModel))]
    [HttpGet]
    public ActionResult<DmrpStatusModel> GetStatus()
    {
        var dmrpEnabled = _settings.Value.Enabled;

        return Ok(new DmrpStatusModel
        {
            DmrpEnabled = dmrpEnabled,
            MockDmrpEnabled = dmrpEnabled && _mockStatus.IsEnabled
        });
    }
}
