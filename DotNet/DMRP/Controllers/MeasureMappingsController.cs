using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.DMRP.Models.Exceptions;
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
        /// Gets a paged list of measure mappings, optionally filtered by measure, dQM or frequency.
        /// </summary>
        /// <remarks>
        /// A measure mapping relates an NHSN measure a facility enrolls in to the digital quality
        /// measure Link evaluates patients against. DMRP reports the NHSN measure only, so this is how
        /// Link translates a reporting plan into something it can schedule.
        /// <para>
        /// Every filter is optional and they combine with AND, so supplying none returns everything.
        /// This is the only read for the collection: there is no unfiltered <c>GET</c> on the route
        /// root, which answers 405.
        /// </para>
        /// </remarks>
        /// <param name="searchDto">
        /// Optional filters and paging. measure and dQM match as case-insensitive substrings, so the
        /// Admin UI can search as the admin types; frequency is one of Discharge,
        /// Daily, Weekly, Monthly or Adhoc. pageSize is 1 to 100 and pageNumber 1 or greater; a value
        /// outside either range is quietly replaced with the default rather than refused.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">A page of measure mappings with its paging metadata.</response>
        /// <response code="204">
        /// Nothing matched. Note this differs from the reporting plans endpoints, which answer an empty
        /// match with 200 and an empty page.
        /// </response>
        /// <response code="500">The measure mappings could not be read.</response>
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
        /// <param name="id">The mapping's own identifier, as returned by create or search.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="200">The measure mapping.</response>
        /// <response code="404">No measure mapping has that Id.</response>
        /// <response code="500">The measure mapping could not be read.</response>
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
        /// <remarks>
        /// Leidos supplies the NHSN measure to dQM mappings; they are maintained here rather than
        /// derived, because the DMRP API's response carries the NHSN measure alone. Until a measure is
        /// mapped, a facility enrolled in it is scheduled for nothing.
        /// <para>
        /// The dQM is verified against MeasureEval before the row is written, so a mapping cannot name
        /// a measure Link could not evaluate. A mapping is unique on measure and dQM.
        /// </para>
        /// </remarks>
        /// <param name="request">
        /// The mapping to create. measure is the NHSN module (for example HOB or HTCDI) and dQM the
        /// digital quality measure it belongs to; both are required and limited to 255 characters.
        /// frequency is Discharge, Daily, Weekly, Monthly or Adhoc, and defaults to Adhoc when omitted
        /// — which schedules nothing, so set it deliberately. Any id in the body is ignored.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="201">The created mapping, with a Location header pointing at it.</response>
        /// <response code="400">
        /// A required field is missing or too long, the dQM is not present in MeasureEval, or a mapping
        /// for that measure and dQM already exists.
        /// </response>
        /// <response code="502">
        /// MeasureEval could not be reached or answered with an error, so the dQM could not be
        /// verified. The mapping was not created; the request can be retried.
        /// </response>
        /// <response code="500">The measure mapping could not be created.</response>
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
        /// <remarks>
        /// Update only. A mapping that does not exist is not created here; the response is 404.
        /// <para>
        /// Reporting plans reference a mapping by Id, so changing its dQM or frequency changes what
        /// every facility already enrolled in that measure gets scheduled for, from the next time a
        /// schedule is derived.
        /// </para>
        /// </remarks>
        /// <param name="id">The mapping to replace. This wins over any id in the body.</param>
        /// <param name="request">
        /// The replacement values, validated exactly as they are on create. id may be omitted from the
        /// body, but if present it must equal the id in the URL.
        /// </param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="202">The updated mapping.</response>
        /// <response code="400">
        /// The body's id does not match the URL, a required field is missing or too long, the dQM is
        /// not present in MeasureEval, or the change would collide with an existing measure and dQM
        /// pair.
        /// </response>
        /// <response code="404">No measure mapping has that Id.</response>
        /// <response code="502">
        /// MeasureEval could not be reached or answered with an error, so the dQM could not be
        /// verified. The mapping was not changed; the request can be retried.
        /// </response>
        /// <response code="500">The measure mapping could not be updated.</response>
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
        /// <remarks>
        /// Reporting plans hold a restricting foreign key to the mapping, so one that facilities are
        /// already enrolled against cannot be removed until those plans are gone. The plan rows are the
        /// record of what DMRP said, and they must not be orphaned by a mapping disappearing.
        /// </remarks>
        /// <param name="id">The mapping to delete.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="204">The mapping was deleted.</response>
        /// <response code="404">No measure mapping has that Id.</response>
        /// <response code="409">
        /// The mapping exists but is referenced by one or more facility reporting plans, so the
        /// database refused to remove it. Delete those reporting plans first.
        /// </response>
        /// <response code="500">The measure mapping could not be deleted.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeasureMapping(string id, CancellationToken cancellationToken)
        {
            id = id.Sanitize();

            try
            {
                await _manager.DeleteAsync(id, cancellationToken);
            }
            catch (MeasureMappingInUseException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
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
        /// <remarks>
        /// Clears the whole table. Intended for resetting a test environment; there is no confirmation
        /// step and no undo. With mappings gone every facility derives an empty schedule, so this is
        /// not something to run against an environment that is reporting.
        /// </remarks>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <response code="204">The mappings were deleted. Deleting an empty table also succeeds.</response>
        /// <response code="409">
        /// One or more mappings are referenced by facility reporting plans, so the database refused
        /// to remove them. Nothing was deleted. Delete the reporting plans first.
        /// </response>
        /// <response code="500">The mappings could not be deleted.</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        [HttpDelete]
        public async Task<IActionResult> DeleteAllMeasureMappings(CancellationToken cancellationToken)
        {
            try
            {
                await _manager.DeleteAllAsync(cancellationToken);
            }
            catch (MeasureMappingInUseException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
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
