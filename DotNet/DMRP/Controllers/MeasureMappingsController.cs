using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Sdk.Clients;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net;

namespace LantanaGroup.Link.DMRP.Controllers
{
    [Route("api/dmrp/measure-mappings")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class MeasureMappingsController : ControllerBase
    {
        private readonly ILogger<MeasureMappingsController> _logger;
        private readonly IMeasureMappingManager _manager;
        private readonly IMeasureMappingQueries _queries;
        private readonly IMeasureEvalServiceClient _measureEvalClient;

        public MeasureMappingsController(
            ILogger<MeasureMappingsController> logger,
            IMeasureMappingManager manager,
            IMeasureMappingQueries queries,
            IMeasureEvalServiceClient measureEvalClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _measureEvalClient = measureEvalClient ?? throw new ArgumentNullException(nameof(measureEvalClient));
        }

        /// <summary>
        /// Get a paged list of measure mappings.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedMeasureMappingDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("search")]
        public async Task<IActionResult> GetMeasureMappings([FromQuery] SearchMeasureMappingDto searchDto, CancellationToken cancellationToken = default)
        {
            searchDto.Sanitize();

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Measure Mappings");

            var result = await _queries.PagedSearchAsync(searchDto, cancellationToken);

            if (result.Records.Count == 0)
            {
                return NoContent();
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets a measure mapping by Id.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeasureMapping(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            var model = await _queries.GetAsync(id, cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return Ok(model);
        }

        /// <summary>
        /// Creates a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        [HttpPost]
        public async Task<IActionResult> CreateMeasureMapping(MeasureMappingModel request, CancellationToken cancellationToken)
        {
            MeasureMapping created;

            try
            {
                var entity = ToEntity(request);
                if (!await DqmExistsAsync(entity.DQM, cancellationToken))
                {
                    return BadRequest($"DQM '{entity.DQM}' was not found in MeasureEval.");
                }

                created = await _manager.CreateAsync(entity, cancellationToken);
            }
            catch (DuplicateMeasureMappingException)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["measure"] = ["A measure mapping for this measure and dQM already exists."]
                }));
            }
            catch (ApplicationException)
            {
                return BadRequest();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "MeasureEval failed while verifying the DQM for a measure mapping.");
                return Problem("Unable to verify the DQM in MeasureEval.", statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.CreateMeasureMapping");
                return Problem("An error occurred while creating the measure mapping", null, 500);
            }

            var model = ToModel(created);

            return Created($"/api/dmrp/measure-mappings/{model.Id}", model);
        }

        /// <summary>
        /// Updates a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(MeasureMappingModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMeasureMapping(string id, MeasureMappingModel request, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            if (request.Id != null && request.Id != id)
            {
                return BadRequest("Id in the URL must match the Id in the request body.");
            }

            try
            {
                var entity = ToEntity(request);
                entity.Id = id;

                if (!await DqmExistsAsync(entity.DQM, cancellationToken))
                {
                    return BadRequest($"DQM '{entity.DQM}' was not found in MeasureEval.");
                }

                await _manager.UpdateAsync(id, entity, cancellationToken);
            }
            catch (DuplicateMeasureMappingException)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["measure"] = ["A measure mapping for this measure and dQM already exists."]
                }));
            }
            catch (ApplicationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "MeasureEval failed while verifying the DQM for a measure mapping.");
                return Problem("Unable to verify the DQM in MeasureEval.", statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.UpdateMeasureMapping");
                return Problem("An error occurred while updating the measure mapping", null, 500);
            }

            var model = await _queries.GetAsync(id, cancellationToken);

            return Accepted(model);
        }

        /// <summary>
        /// Deletes a measure mapping.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeasureMapping(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            try
            {
                await _manager.DeleteAsync(id, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.DeleteMeasureMapping");
                return Problem("An error occurred while deleting the measure mapping", null, 500);
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes all measure mappings.
        /// </summary>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        [HttpDelete]
        public async Task<IActionResult> DeleteAllMeasureMappings(CancellationToken cancellationToken)
        {
            try
            {
                await _manager.DeleteAllAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in MeasureMappingsController.DeleteAllMeasureMappings");
                return Problem("An error occurred while deleting the measure mappings", null, 500);
            }

            return NoContent();
        }

        private static MeasureMapping ToEntity(MeasureMappingModel model) => new()
        {
            Measure = model.Measure?.Sanitize() ?? "",
            DQM = model.DQM?.Sanitize() ?? "",
            Frequency = model.Frequency ?? Frequency.Adhoc
        };

        private static MeasureMappingModel ToModel(MeasureMapping entity) => new()
        {
            Id = entity.Id,
            Measure = entity.Measure,
            DQM = entity.DQM,
            Frequency = entity.Frequency
        };

        private async Task<bool> DqmExistsAsync(string dqm, CancellationToken cancellationToken)
        {
            var response = await _measureEvalClient.GetMeasureDefinitionAsync(dqm, cancellationToken);

            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"MeasureEval returned status code {response.StatusCode} while verifying a DQM.", null, (HttpStatusCode)response.StatusCode);
            }

            return true;
        }
    }
}
