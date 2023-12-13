using LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using static Google.Rpc.Context.AttributeContext.Types;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportConfigController : ControllerBase
    {
        private readonly ILogger<ReportConfigController> _logger;
        private readonly IMediator _mediator;

        public ReportConfigController(ILogger<ReportConfigController> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        /// <summary>
        /// CGet the MeasureReportConfig for the provided Id
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpGet("Get")]
        public async Task<MeasureReportConfigModel> GetReportConfig(string Id)
        {
            var res = await _mediator.Send(new GetMeasureReportConfigQuery { Id = Id });

            return res;
        }

        /// <summary>
        /// Creates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpGet("facility/{facilityId}")]
        public async Task<IEnumerable<MeasureReportConfigModel>> GetReportConfigForFacilityId(string facilityId)
        {
            var res = (await _mediator.Send(new GetFacilityMeasureReportConfigsQuery { FacilityId = facilityId })).ToList();

            return res;
        }

        /// <summary>
        /// Creates a MeasureReportConfig entity with the specified data.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPost("Create")]
        public async Task<IActionResult> CreateReportConfig([FromBody] MeasureReportConfig config)
        {
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
        public async Task<MeasureReportConfigModel> UpdateReportConfig([FromBody] MeasureReportConfig config)
        {
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