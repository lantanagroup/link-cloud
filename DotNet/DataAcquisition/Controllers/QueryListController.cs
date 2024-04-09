using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/")]
[ApiController]
public class QueryListController : Controller
{
    private readonly ILogger<QueryConfigController> _logger;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;

    public QueryListController(ILogger<QueryConfigController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;
    }

    [HttpGet("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfiguration>> GetFhirConfiguration(string facilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            var result = await _mediator.Send(new GetFhirListConfigQuery
            {
                FacilityId = facilityId
            });

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving FhirQueryConfiguration for facility {facilityId}");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Creates or updates a FhirQueryConfiguration record for a given facilityId.
    /// Supported Authentication Types: Basic, Epic
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="fhirListConfiguration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("{facilityId}/fhirQueryList")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FhirListConfiguration))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FhirListConfiguration>> PostFhirConfiguration(string facilityId, [FromBody] FhirListConfiguration fhirListConfiguration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return BadRequest();
        }

        try
        {
            await _mediator.Send(new SaveFhirListCommand
            {
                FacilityId = facilityId,
                FhirListConfiguration = fhirListConfiguration
            });

            return Ok();
        }
        catch (MissingFacilityConfigurationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving FhirQueryConfiguration for facility {facilityId}");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
