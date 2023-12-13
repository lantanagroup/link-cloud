using LantanaGroup.Link.DataAcquisition.Application.Commands.Config;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data/config")]
[ApiController]
public class ConfigController : Controller
{
    private readonly ILogger<ConfigController> _logger;
    private readonly IMediator _mediator;

    public ConfigController(ILogger<ConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Gets TenantDataAcquisitionConfigModel for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 200
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpGet("{facilityId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantDataAcquisitionConfigModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TenantDataAcquisitionConfigModel>> GetConfig(string facilityId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"GET request for facility {facilityId}");
        var config = await _mediator.Send(new GetConfigQuery { FacilityId = facilityId });
        if(config == null)
        {
            return NotFound();
        }

        return Ok(config);
    }

    /// <summary>
    /// Creates a TenantDataAcquisitionConfigModel for a facility
    /// </summary>
    /// <param name="tenantData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 201
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Facility Already Exists: 409
    ///     Server Error: 500
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateConfig([FromBody] TenantDataAcquisitionConfigModel tenantData, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"POST request for tenant {tenantData.TenantId}");
        var success = false;
        try
        {
            success = await upsertRecord(tenantData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message,ex.InnerException);
            return StatusCode(500);
        }

        if (success) { return Accepted(); }
        return StatusCode(500);
    }

    /// <summary>
    /// Updates a TenantDataAcquisitionConfigModel for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="tenantData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpPut("{facilityId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfig(string facilityId, [FromBody] TenantDataAcquisitionConfigModel tenantData, CancellationToken cancellationToken = default) 
    {
        _logger.LogDebug($"PUT request for facility {facilityId}");
        var success = false;
        try
        {
            success = await upsertRecord(tenantData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex.InnerException);
            return StatusCode(500);
        }

        if (success) { return Accepted(); }
        return StatusCode(500);
    }

    /// <summary>
    /// Hard deletes a TenantDataAcquisitionConfigModel for a given facilityId.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Success: 202
    ///     Bad Facility ID: 404
    ///     Missing Facility ID: 400
    ///     Server Error: 500
    /// </returns>
    [HttpDelete("{facilityId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfig(string facilityId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"DELETE request for facility {facilityId}");
        try
        {
            await _mediator.Send(new DeleteConfigCommand { FacilityId = facilityId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex.InnerException);
            return StatusCode(500);
        }

        return Accepted(); 
    }

    private async Task<bool> upsertRecord(TenantDataAcquisitionConfigModel tenantData, CancellationToken cancellationToken = default)
    {
        try
        {
            await _mediator.Send(new SaveConfigCommand { TenantDataAcquisitionConfigModel = tenantData });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex.Message, ex.InnerException);
            return false;
        }
        return true;
    }

}
