using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
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
        private readonly CopyPropertyOperationService _copyPropertyOperationService;
        private readonly CodeMapOperationService _codeMapOperationService;
        private readonly ConditionalTransformOperationService _conditionalTransformOperationService;

        public OperationsController(IOperationManager operationManager, IOperationQueries operationQueries, ITenantApiService tenantApiService, CopyPropertyOperationService copyPropertyService, CodeMapOperationService codeMapOperationService, ConditionalTransformOperationService conditionalTransformOperationService)
        {
            _operationManager = operationManager;
            _operationQueries = operationQueries;
            _tenantApiService = tenantApiService;
            _copyPropertyOperationService = copyPropertyService;
            _codeMapOperationService = codeMapOperationService;
            _conditionalTransformOperationService = conditionalTransformOperationService;
        }

        private object? GetOperationImplementation(IOperation operation)
        {
            return operation.OperationType switch
            {
                OperationType.CopyProperty => (object)(CopyPropertyOperation)operation,
                OperationType.CodeMap => (object)(CodeMapOperation)operation,
                OperationType.ConditionalTransform => (object)(ConditionalTransformOperation)operation,
                _ => null
            };
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

                var operationType = model.Operation.OperationType;

                var operationImplementation = GetOperationImplementation(model.Operation);

                if (operationImplementation == null)
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
                    OperationType = operationType.ToString(),
                    OperationJson = JsonSerializer.Serialize(operationImplementation),
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OperationResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> OperationTest([FromBody] TestOperationModel model)
        {
            try
            {
                if (model.Operation == null)
                {
                    return BadRequest("TestOperationModel.Operation cannot be null.");
                }

                if (string.IsNullOrEmpty(model.Resource))
                {
                    return BadRequest("TestOperationModel.Resource cannot be null.");
                }

                var operationType = model.Operation.OperationType;

                var operationImplementation = GetOperationImplementation(model.Operation);
                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                FhirJsonParser? _fhirJsonParser = new FhirJsonParser();
                var domainResource = (DomainResource)_fhirJsonParser.Parse(model.Resource);

                OperationResult? result = model.Operation.OperationType switch
                {
                    OperationType.CopyProperty => await _copyPropertyOperationService.EnqueueOperationAsync((CopyPropertyOperation)operationImplementation, domainResource),
                    OperationType.CodeMap => await _codeMapOperationService.EnqueueOperationAsync((CodeMapOperation)operationImplementation, domainResource),
                    OperationType.ConditionalTransform => await _conditionalTransformOperationService.EnqueueOperationAsync((ConditionalTransformOperation)operationImplementation, domainResource),
                    _ => null
                };

                if(result.SuccessCode == OperationStatus.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return Problem(result.ErrorMessage, statusCode: StatusCodes.Status422UnprocessableEntity);
                }                
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

                var operationImplementation = GetOperationImplementation(model.Operation);

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
                    OperationJson = JsonSerializer.Serialize(operationImplementation),
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
