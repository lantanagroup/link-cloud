using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using static Google.Rpc.Context.AttributeContext.Types;
using static Hl7.Fhir.Model.Bundle;

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
        public async Task<ActionResult<IEnumerable<MeasureReportConfig>>> GetReportConfigForFacilityId(string facilityId)
        {
            var  res = (await _mediator.Send(new GetFacilityMeasureReportConfigsQuery { FacilityId = facilityId })).ToList();
            if (res == null) return NoContent();
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
        public async Task<IActionResult> CreateReportConfig([FromBody] MeasureReportConfig config)
        {
            if (!await _tenantApiService.CheckFacilityExists(config.FacilityId))
            {
                return BadRequest($"Tenant {config.FacilityId} does not exist.");
            }

            BundlingType bundleType = BundlingType.Default;
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
                return Ok(entity);
            }
            else if (returned != null)
            {
                return BadRequest(
                    "ReportConfigController.CreateReportConfig: An error was encountered during creation.");
            }
            else
            {
                return BadRequest(
                    "ReportConfigController.CreateReportConfig: A MeasureReportConfig with that Id already exists");
            }
        }

        /// <summary>
        /// Updates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPut("Update")]
        public async Task<ActionResult<MeasureReportConfigModel>> UpdateReportConfig([FromBody] MeasureReportConfig config)
        {
            if (!await _tenantApiService.CheckFacilityExists(config.FacilityId))
            {
                return BadRequest($"Tenant {config.FacilityId} does not exist.");
            }

            BundlingType bundleType = BundlingType.Default;
            var entity = new MeasureReportConfigModel()
            {
                Id = config.Id,
                FacilityId = config.FacilityId,
                ReportType = config.ReportType,
                BundlingType = BundlingType.TryParse(config.BundlingType, out bundleType) ? bundleType : BundlingType.Default
            };

            await _mediator.Send(new UpdateMeasureReportConfigCommand
            {
                MeasureReportConfig = entity
            });

            return entity;
        }

        /// <summary>
        /// Deletes the MeasureReportConfig for the specified Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("Delete")]
        public async Task<IActionResult> DeleteReportConfig(string Id)
        {
            await _mediator.Send(new DeleteMeasureReportConfigCommand()
            {
                Id = Id
            });
            
            return Ok();
        }
    }
}