using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/normalization/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class OperationsController : ControllerBase
    {
        private readonly IOperationManager _operationManager;
        private readonly IOperationQueries _operationQueries;
        private readonly IVendorQueries _vendorQueries;
        private readonly ITenantApiService _tenantApiService;
        private readonly CopyPropertyOperationService _copyPropertyOperationService;
        private readonly CodeMapOperationService _codeMapOperationService;
        private readonly ConditionalTransformOperationService _conditionalTransformOperationService;

        public OperationsController(IOperationManager operationManager, IOperationQueries operationQueries, IVendorQueries vendorQueries, ITenantApiService tenantApiService, CopyPropertyOperationService copyPropertyService, CodeMapOperationService codeMapOperationService, ConditionalTransformOperationService conditionalTransformOperationService)
        {
            _operationManager = operationManager;
            _operationQueries = operationQueries;
            _vendorQueries = vendorQueries;
            _tenantApiService = tenantApiService;
            _copyPropertyOperationService = copyPropertyService;
            _codeMapOperationService = codeMapOperationService;
            _conditionalTransformOperationService = conditionalTransformOperationService;
        }
        
        [HttpGet("")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OperationModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Policy = PolicyNames.IsLinkAdmin)]
        public async Task<ActionResult<PagedConfigModel<OperationModel>>> SearchOperations(string? facilityId, string? operationType, string? resourceType, Guid? operationId, bool includeDisabled = false, Guid? vendorId = null,
            string sortBy = "CreateDate", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1)
        {
            try
            {
                if(!string.IsNullOrEmpty(facilityId)) 
                {
                    if(!await _tenantApiService.CheckFacilityExists(facilityId))
                    {
                        return BadRequest($"Provided FacilityID {facilityId.SanitizeAndRemove()} does not exist");
                    }
                }

                operationType = string.IsNullOrEmpty(operationType) ? null : operationType;

                OperationType operation = OperationType.None;

                if (operationType != null && !Enum.TryParse(operationType, ignoreCase: true, out operation))
                {
                    return BadRequest($"'{operationType}' is not a valid OperationType.");
                }

                var result = await _operationQueries.Search(new OperationSearchModel
                {
                    OperationId = operationId,
                    OperationType = operation == OperationType.None ? null : operation,
                    FacilityId = string.IsNullOrWhiteSpace(facilityId) ? null : facilityId,
                    VendorId = vendorId,
                    IncludeDisabled = includeDisabled,
                    ResourceType = resourceType,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    PageSize = pageSize,
                    PageNumber = pageNumber
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OperationModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<OperationModel>>> GetOperations(string facilityId, string? operationType = null, string? resourceType = default, Guid? operationId = default, bool includeDisabled = false, Guid? vendorId = null,
            string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(facilityId))
                {
                    return BadRequest($"A faciityId must be provided");
                }

                if (!await _tenantApiService.CheckFacilityExists(facilityId))
                {
                    return BadRequest($"Provided FacilityID {facilityId.SanitizeAndRemove()} does not exist");
                }

                operationType = string.IsNullOrEmpty(operationType) ? null : operationType;

                OperationType operation = OperationType.None;

                if (operationType != null && !Enum.TryParse(operationType, ignoreCase: true, out operation))
                {
                    return BadRequest($"'{operationType}' is not a valid OperationType.");
                }

                var result = await _operationQueries.Search(new OperationSearchModel
                {
                    OperationId = operationId,
                    OperationType = operation == OperationType.None ? null : operation,
                    FacilityId = string.IsNullOrEmpty(facilityId) ? null : facilityId,
                    VendorId = vendorId,
                    IncludeDisabled = includeDisabled,
                    ResourceType = resourceType,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    PageSize = pageSize,
                    PageNumber = pageNumber
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("vendor/{vendor}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OperationModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<OperationModel>>> GetVendorOperations(string vendor, string? operationType = null, string? resourceType = default, Guid? operationId = default, bool includeDisabled = false,
            string sortBy = "Id", SortOrder sortOrder = SortOrder.Descending, int pageSize = 10, int pageNumber = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(vendor))
                {
                    return BadRequest($"A vendor Name or Id must be provided");
                }

                VendorModel? foundVendor;
                if (Guid.TryParse(vendor, out var vendorId))
                {
                    foundVendor = await _vendorQueries.GetVendor(vendorId);
                }
                else
                {
                    foundVendor = await _vendorQueries.GetVendor(vendor);

                    if (foundVendor == null)
                    {
                        return base.BadRequest($"No vendor by the name {vendor.Sanitize()} found.");
                    }
                }

                operationType = string.IsNullOrEmpty(operationType) ? null : operationType;

                OperationType operation = OperationType.None;

                if (operationType != null && !Enum.TryParse(operationType, ignoreCase: true, out operation))
                {
                    return BadRequest($"'{operationType}' is not a valid OperationType.");
                }

                var result = await _operationQueries.Search(new OperationSearchModel
                {
                    OperationId = operationId,
                    OperationType = operation == OperationType.None ? null : operation,
                    VendorId = foundVendor.Id,
                    FacilityId = null,
                    IncludeDisabled = includeDisabled,
                    ResourceType = resourceType,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    PageSize = pageSize,
                    PageNumber = pageNumber
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(OperationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostOperation([FromBody] PostOperationModel model)
        {
            try
            {
                if (model.Operation == null)
                {
                    return BadRequest("PostOperationModel.Operation cannot be null.");
                }

                if (model.ResourceTypes == null || model.ResourceTypes.Count == 0)
                {
                    return BadRequest("PostOperationModel.ResourceTypes cannot be null or empty.");
                }

                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    if (!await _tenantApiService.CheckFacilityExists(model.FacilityId))
                    {
                        return BadRequest($"Provided FacilityID {model.FacilityId.SanitizeAndRemove()} does not exist");
                    }
                }

                var operationType = model.Operation.OperationType;

                var operationImplementation = OperationServiceHelper.GetOperationImplementation(model.Operation);               

                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }            

                var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
                {
                    OperationType = operationType.ToString(),
                    OperationJson = JsonSerializer.Serialize(operationImplementation),
                    ResourceTypes = model.ResourceTypes,
                    FacilityId = model.FacilityId == string.Empty ? null : model.FacilityId,
                    Description = model.Description,
                    VendorIds = model.VendorIds
                });

                if(!taskResult.IsSuccess)
                {
                    return Problem(detail: taskResult.ErrorMessage, statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                return Created("", taskResult.ObjectResult);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("")]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(OperationModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutOperation([FromBody] PutOperationModel model)
        {
            try
            {
                if (model.Id == null)
                {
                    return BadRequest("PutOperationModel.Id cannot be null.");
                }

                if (model.Operation == null)
                {
                    return BadRequest("PutOperationModel.Operation cannot be null.");
                }

                if (model.ResourceTypes == null || model.ResourceTypes.Count == 0)
                {
                    return BadRequest("PutOperationModel.ResourceTypes cannot be null or empty.");
                }

                if (!string.IsNullOrEmpty(model.FacilityId))
                {
                    if (!await _tenantApiService.CheckFacilityExists(model.FacilityId))
                    {
                        return BadRequest($"Provided FacilityID {model.FacilityId.SanitizeAndRemove()} does not exist");
                    }
                }

                var operationImplementation = OperationServiceHelper.GetOperationImplementation(model.Operation);

                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                var taskResult = await _operationManager.UpdateOperation(new UpdateOperationModel()
                {
                    Id = model.Id,
                    OperationJson = JsonSerializer.Serialize(operationImplementation),
                    ResourceTypes = model.ResourceTypes,
                    FacilityId = string.IsNullOrWhiteSpace(model.FacilityId) ? null : model.FacilityId,
                    Description = model.Description,
                    IsDisabled = model.IsDisabled,
                    VendorIds = model.VendorIds
                });

                if (!taskResult.IsSuccess)
                {
                    return Problem(detail: taskResult.ErrorMessage, statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                return Accepted("", taskResult.ObjectResult);
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

                if (model.Resource == null)
                {
                    return BadRequest("TestOperationModel.Resource cannot be null.");
                }

                var operationType = model.Operation.OperationType;

                var operationImplementation = OperationServiceHelper.GetOperationImplementation(model.Operation);
                if (operationImplementation == null)
                {
                    return BadRequest("Operation did not match any existing Operation Types.");
                }

                var domainResource = model.Resource;

                OperationResult? result = model.Operation.OperationType switch
                {
                    OperationType.CopyProperty => await _copyPropertyOperationService.EnqueueOperationAsync((CopyPropertyOperation)operationImplementation, domainResource),
                    OperationType.CodeMap => await _codeMapOperationService.EnqueueOperationAsync((CodeMapOperation)operationImplementation, domainResource),
                    OperationType.ConditionalTransform => await _conditionalTransformOperationService.EnqueueOperationAsync((ConditionalTransformOperation)operationImplementation, domainResource),
                    _ => null
                };

                if (result.SuccessCode == OperationStatus.Success)
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

        [HttpPost("{id}/test")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OperationResult))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> OperationTest(Guid id, [FromBody] string resource, string? facilityId = null)
        {
            try
            {
                var dbEntity = await _operationQueries.Get(id, facilityId);

                if(dbEntity == null)
                {
                    return NotFound($"No Operation found for ID {HtmlInputSanitizer.Sanitize(id.ToString())}");
                }

                var operation = OperationHelper.GetOperation(dbEntity.OperationType, dbEntity.OperationJson);

                if (operation == null)
                {
                    throw new Exception("Operation entity found, but a configuration or deserialization issue occurred.");
                }

                var fhirJsonParser = new FhirJsonParser();
                var domainResource = (DomainResource)await fhirJsonParser.ParseAsync(resource);

                OperationResult? result = operation.OperationType switch
                {
                    OperationType.CopyProperty => await _copyPropertyOperationService.EnqueueOperationAsync((CopyPropertyOperation)operation, domainResource),
                    OperationType.CodeMap => await _codeMapOperationService.EnqueueOperationAsync((CodeMapOperation)operation, domainResource),
                    OperationType.ConditionalTransform => await _conditionalTransformOperationService.EnqueueOperationAsync((ConditionalTransformOperation)operation, domainResource),
                    _ => null
                };

                if (result.SuccessCode == OperationStatus.Success)
                {
                    return Ok(result);
                }
                else if(operation.OperationType == OperationType.ConditionalTransform && result.SuccessCode == OperationStatus.NoAction)
                {
                    return Problem(detail: result.ErrorMessage, statusCode: StatusCodes.Status204NoContent);
                }
                else
                {
                    return Problem(detail: result.ErrorMessage, statusCode: StatusCodes.Status422UnprocessableEntity);
                }
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteFacilityOperations(string facilityId, Guid? operationId = null, string? resourceType = null)
        {
            try
            {
                if (string.IsNullOrEmpty(facilityId) && operationId == null)
                {
                    return BadRequest("Request must include a valid facilityId and/or operationId");
                }

                var result = await _operationManager.DeleteOperation(new DeleteOperationModel()
                {
                    FacilityId = facilityId,
                    OperationId = operationId,
                    ResourceType = resourceType
                });

                if (result)
                {
                    return NoContent();
                }
                else
                {
                    return Problem("No records were deleted.", statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("vendor/{vendor}")]        
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteVendorOperations(string vendor, Guid? operationId = null, string? resourceType = null)
        {
            try
            {
                VendorModel? foundVendor;
                if (Guid.TryParse(vendor, out var vendorId))
                {
                    foundVendor = await _vendorQueries.GetVendor(vendorId);
                }
                else
                {
                    foundVendor = await _vendorQueries.GetVendor(vendor);

                    if (foundVendor == null)
                    {
                        return base.BadRequest($"No vendor by the name {vendor.Sanitize()} found.");
                    }
                }

                var result = await _operationManager.DeleteOperation(new DeleteOperationModel()
                {
                    VendorId = foundVendor.Id,
                    OperationId = operationId,
                    ResourceType = resourceType
                });

                if (result)
                {
                    return NoContent();
                }
                else
                {
                    return Problem("No records were deleted.", statusCode: StatusCodes.Status404NotFound);
                }
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
