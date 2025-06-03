using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/Normalization/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class OperationSequenceController : ControllerBase
    {
        private readonly IOperationManager _operationManager;
        private readonly IOperationSequenceQueries _operationSequenceQueries;
        private readonly ITenantApiService _tenantApiService;

        public OperationSequenceController(IOperationManager operationManager, IOperationSequenceQueries operationSequenceQueries, ITenantApiService tenantApiService)
        {
            _operationManager = operationManager;
            _operationSequenceQueries = operationSequenceQueries;
            _tenantApiService = tenantApiService;
        }

        [HttpGet("")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OperationSequenceModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOperationSequence(string facilityId, string? resourceType = null, Guid? resourceTypeId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(facilityId))
                {
                    var exists = await _tenantApiService.CheckFacilityExists(facilityId);

                    if (!exists)
                    {
                        return BadRequest("No Facility exists for the provided FacilityId.");
                    }
                }
                else
                {
                    return BadRequest("A FacilityId must be provided");
                }

                var results = await _operationSequenceQueries.Search(new OperationSequenceSearchModel()
                {
                    ResourceType = resourceType,
                    ResourceTypeId = resourceTypeId,
                    FacilityId = facilityId
                });

                if(results == null || results.Count == 0)
                {
                    return Problem("No Operations found.", statusCode: StatusCodes.Status404NotFound);
                }

                return Ok(results);
            }
            catch(Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(List<OperationSequenceModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostOperationSequences(string facilityId, string resourceType, [FromBody] List<PostOperationSequence> model)
        {
            try
            {
                if (!string.IsNullOrEmpty(facilityId))
                {
                    var exists = await _tenantApiService.CheckFacilityExists(facilityId);

                    if (!exists)
                    {
                        return BadRequest("No Facility exists for the provided FacilityId.");
                    }
                }
                else
                {
                    return BadRequest("A FacilityId must be provided");
                }

                if(model == null || model.Count == 0)
                {
                    return BadRequest("At least one Operation ID must be provided");
                }


                var sequences = await _operationManager.CreateOperationSequences(new CreateOperationSequencesModel()
                {
                    FacilityId = facilityId,
                    ResourceType = resourceType,
                    OperationSequences = model.Select(a => new CreateOperationSequenceModel
                    {
                        OperationId = a.OperationId,
                        Sequence = a.Sequence
                    }).ToList()
                });


                return Created("", sequences);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOperationSequences(string facilityId, string? resourceType)
        {
            try
            {
                if (!string.IsNullOrEmpty(facilityId))
                {
                    var exists = await _tenantApiService.CheckFacilityExists(facilityId);

                    if (!exists)
                    {
                        return BadRequest("No Facility exists for the provided FacilityId.");
                    }
                }
                else
                {
                    return BadRequest("A FacilityId must be provided");
                }

                var deleted = await _operationManager.DeleteOperationSequence(new DeleteOperationSequencesModel()
                {
                    FacilityId = facilityId,
                    ResourceType = resourceType
                });

                if(deleted)
                    return Accepted();

                return NotFound();
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
