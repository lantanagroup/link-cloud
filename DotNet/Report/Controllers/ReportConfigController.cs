using LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportConfigController : ControllerBase
    {
        private readonly ILogger<ReportConfigController> _logger;
        private readonly IMediator _mediator;
        private readonly ITenantApiService _tenantApiService;
        public ReportConfigController(ILogger<ReportConfigController> logger, IMediator mediator, ITenantApiService tenantApiService)
        {
            _logger = logger;
            _mediator = mediator;
            _tenantApiService = tenantApiService;
        }

        /// <summary>
        /// CGet the MeasureReportConfig for the provided Id
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpGet("Get")]
        public async Task<ActionResult<MeasureReportConfig>> GetReportConfig(string Id)
        {
            var response = await _mediator.Send(new GetMeasureReportConfigQuery { Id = Id });
            if (response == null) return NoContent();
            var model = new MeasureReportConfig()
            {
                Id = response.Id,
                FacilityId = response.FacilityId,
                ReportType = response.ReportType,
                BundlingType = response.BundlingType.ToString()
            };
            return Ok(model);
        }

        /// <summary>
        /// Creates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpGet("facility/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MeasureReportConfig>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<MeasureReportConfig>>> GetReportConfigForFacilityId(string facilityId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest("FacilityId is null or empty");
            }

            var  res = (await _mediator.Send(new GetFacilityMeasureReportConfigsQuery { FacilityId = facilityId })).ToList();
            if (res == null)
            {
                return Problem($"No MeasureReportConfigs found for FacilityId {facilityId}", statusCode: 304);
            }

            var  list = res.Select(model => new MeasureReportConfig()
            {
                Id = model.Id,
                FacilityId = model.FacilityId,
                ReportType = model.ReportType,
                BundlingType = model.BundlingType.ToString()
            });

            return Ok(list);
        }

        /// <summary>
        /// Creates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPost("Create")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MeasureReportConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MeasureReportConfig>> CreateReportConfig([FromBody] MeasureReportConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.FacilityId))
            {
                return BadRequest("FacilityId is null or empty");
            }

            if (!await _tenantApiService.CheckFacilityExists(config.FacilityId))
            {
                return BadRequest($"Tenant {config.FacilityId} does not exist.");
            }

            if (string.IsNullOrWhiteSpace(config.ReportType))
            {
                return BadRequest("ReportType is null or empty");
            }

            BundlingType bundleType;
            var entity = new MeasureReportConfigModel()
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = config.FacilityId,
                ReportType = config.ReportType,
                BundlingType = BundlingType.TryParse(config.BundlingType, out bundleType) ? bundleType : BundlingType.Default
            };

            var returned = await _mediator.Send(new CreateMeasureReportConfigCommand
            {
                MeasureReportConfig = entity
            });

            if (returned != null && !string.IsNullOrWhiteSpace(returned.Id))
            {
                return Created(returned.Id, returned);
            }
            else if (returned != null)
            {
                return Problem("Unable to create the MeasureReportConfig", statusCode: 304);
            }
            else
            {
                return Problem(
                    "ReportConfigController.CreateReportConfig: A MeasureReportConfig with that Id already exists", statusCode: 304);
            }
        }

        /// <summary>
        /// Updates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPut("Update")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MeasureReportConfigModel))]
        [ProducesResponseType(StatusCodes.Status304NotModified, Type = typeof(MeasureReportConfigModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MeasureReportConfigModel>> UpdateReportConfig([FromBody] MeasureReportConfig config)
        {
            if (!await _tenantApiService.CheckFacilityExists(config.FacilityId))
            {
                return BadRequest($"Tenant {config.FacilityId} does not exist.");
            }

            BundlingType bundleType;
            var entity = new MeasureReportConfigModel()
            {
                Id = config.Id,
                FacilityId = config.FacilityId,
                ReportType = config.ReportType,
                BundlingType = BundlingType.TryParse(config.BundlingType, out bundleType) ? bundleType : BundlingType.Default
            };

            var updatedConfig = await _mediator.Send(new UpdateMeasureReportConfigCommand
            {
                MeasureReportConfig = entity
            });

            if (updatedConfig != null)
            {
                return Ok(updatedConfig);
            }
            else
            {
                return Problem($"TenantSubmissionConfig {config.Id} not found.", statusCode: 304, type: typeof(MeasureReportConfig).ToString());
            }
        }

        /// <summary>
        /// Deletes the MeasureReportConfig for the specified Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("Delete")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<bool>> DeleteReportConfig(string Id, CancellationToken cancellationToken = default)
        {

            

            if (string.IsNullOrWhiteSpace(Id))
            {
                return BadRequest("Id is null or white space.");
            }

            try
            {
                var retVal = await _mediator.Send(new DeleteMeasureReportConfigCommand()
                {
                    Id = Id
                });

                return Ok(retVal);
            }
            catch { }

            return Problem($"TenantSubmissionConfig {configId} not found.", statusCode: 304);
        }
    }
}