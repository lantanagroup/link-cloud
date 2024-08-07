using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly MeasureReportSubmissionBundler _bundler;
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;

        public ReportController(ILogger<ReportController> logger, MeasureReportSubmissionBundler bundler, PatientReportSubmissionBundler patientReportSubmissionBundler)
        {
            _logger = logger;
            _bundler = bundler;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
        }

        /// <summary>
        /// Generates the bundle for the report indicated by the provided parameters.
        /// </summary>
        /// <param name="reportId"></param>
        /// <returns></returns>
        [HttpGet("Bundle/MeasureReport")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(JsonElement))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MeasureReportSubmissionModel>> GetSubmissionBundle(string reportId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reportId))
                {
                    BadRequest("Paramater reportId is null or whitespace");
                }

                _logger.LogInformation($"Executing GenerateSubmissionBundleJob for MeasureReportScheduleModel {reportId}");

                MeasureReportSubmissionModel submission = await _bundler.GenerateBundle(reportId);

                return Ok(submission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ReportController.GetSubmissionBundle for ReportId {reportId}: {ex.Message}");
                return Problem(ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Returns a serialized PatientSubmissionModel containing all of the Patient level resources and Other resources
        /// for all measure reports for the provided FacilityId, PatientId, and Reporting Period.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="patientId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        [HttpGet("Bundle/Patient")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PatientSubmissionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PatientSubmissionModel>> GetSubmissionBundleForPatient(string facilityId, string patientId, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    BadRequest("Paramater facilityId is null or whitespace");
                }

                if (string.IsNullOrWhiteSpace(patientId))
                {
                    BadRequest("Paramater patientId is null or whitespace");
                }

                var submission = await _patientReportSubmissionBundler.GenerateBundle(facilityId, patientId, startDate, endDate);

                return Ok(submission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ReportController.GetSubmissionBundleForPatient for PatientId {patientId}: {ex.Message}");
                return Problem(ex.Message, statusCode: 500);
            }
        }
    }
}