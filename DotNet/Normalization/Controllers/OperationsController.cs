using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/Normalization/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class OperationsController : ControllerBase
    {
        private readonly IOperationManager _operationManager;
        private readonly IOperationQueries _operationQueries;
        private readonly ITenantApiService _tenantApiService;

        public OperationsController(IOperationManager operationManager, IOperationQueries operationQueries, ITenantApiService tenantApiService)
        {
            _operationManager = operationManager;
            _operationQueries = operationQueries;
            _tenantApiService = tenantApiService;
        }

        [HttpGet("")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OperationModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOperation(string operationType, string? facilityId = default)
        {
            try
            {
                if (!Enum.TryParse(operationType, ignoreCase: true, out OperationType operation))
                {
                    return BadRequest($"'{operationType}' is not a valid OperationType.");
                }

                var result = await _operationQueries.Search(new OperationSearchModel()
                {
                    OperationType = operation,
                    FacilityId = facilityId
                });

                if(result == null || result.Count == 0)
                {
                    return Problem("No Operations found.", statusCode: StatusCodes.Status404NotFound);
                }

                return Ok(result);
            }
            catch(Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(OperationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostOperation([FromBody] PostOperationModel model)
        {
            try
            {
                if(model.Operation == null)
                {
                    return BadRequest("PostOperationModel.Operation cannot be null.");
                }

                if (model.ResourceTypes == null || model.ResourceTypes.Count == 0)
                {
                    return BadRequest("PostOperationModel.ResourceTypes cannot be null or empty.");
                }

                var operationImplementation = model.Operation.OperationType switch
                {
                    OperationType.CopyProperty => (CopyPropertyOperation)model.Operation,
                    //TODO: Fill in other operations
                    _ => null
                };

                if(operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                if(!string.IsNullOrEmpty(model.FacilityId))
                {
                   var exists = await _tenantApiService.CheckFacilityExists(model.FacilityId);   
                    
                    if(!exists)
                    {
                        return BadRequest("No Facility exists for the provided FacilityId.");
                    }
                }

                var operation = await _operationManager.CreateOperation(new CreateOperationModel()
                {
                    OperationType = operationImplementation.OperationType.ToString(),
                    OperationJson = JsonSerializer.Serialize<object>(operationImplementation),
                    ResourceTypes = model.ResourceTypes,
                    FacilityId = model.FacilityId,
                    Description = model.Description,
                });


                return Created("", operation);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("test")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DomainResource))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult OperationTest([FromBody] TestOperationModel model)
        {
            try
            {
                if (model.Operation == null)
                {
                    return BadRequest("TestOperationModel.Operation cannot be null.");
                }

                if (string.IsNullOrEmpty(model.ResourceType))
                {
                    return BadRequest("TestOperationModel.ResourceType cannot be null or empty.");
                }

                if (model.Resource == null)
                {
                    return BadRequest("TestOperationModel.Resource cannot be null.");
                }

                var operationImplementation = model.Operation.OperationType switch
                {
                    OperationType.CopyProperty => (CopyPropertyOperation)model.Operation,
                    _ => null
                };

                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                var resource = operationImplementation.Execute(model.Resource);


                return Ok(resource);
                
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("")]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(OperationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutOperation([FromBody] PutOperationModel model)
        {
            try
            {
                if (model.Operation == null)
                {
                    return BadRequest("PutOperationModel.Operation cannot be null.");
                }

                if (model.ResourceTypes == null || model.ResourceTypes.Count == 0)
                {
                    return BadRequest("PutOperationModel.ResourceTypes cannot be null or empty.");
                }

                var operationImplementation = model.Operation.OperationType switch
                {
                    OperationType.CopyProperty => (CopyPropertyOperation)model.Operation,
                    _ => null
                };

                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    var exists = await _tenantApiService.CheckFacilityExists(model.FacilityId);

                    if (!exists)
                    {
                        return BadRequest("No Facility exists for the provided FacilityId.");
                    }
                }

                var operation = await _operationManager.UpdateOperation(new UpdateOperationModel()
                {
                    Id = model.Id,
                    OperationJson = JsonSerializer.Serialize<object>(operationImplementation),
                    ResourceTypes = model.ResourceTypes,
                    FacilityId = model.FacilityId,
                    Description = model.Description,
                    IsDisabled = model.IsDisabled
                });


                return Accepted("", operation);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
