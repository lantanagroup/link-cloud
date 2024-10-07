using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LantanaGroup.Link.Report.Domain.Managers;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly ISubmissionEntryManager _submissionEntryManager;

        public ReportController(ILogger<ReportController> logger, PatientReportSubmissionBundler patientReportSubmissionBundler, ISubmissionEntryManager submissionEntryManager)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _submissionEntryManager = submissionEntryManager;
        }

        /// <summary>
        /// Returns a serialized PatientSubmissionModel containing all of the Patient level resources and Other resources
        /// for all measure reports for the provided FacilityId, PatientId, and Reporting Period.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="patientId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        [HttpPost("Bundle/Patient")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<PatientSubmissionModel>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PatientSubmissionModel>> GetSubmissionBundleForPatient(string facilityId, string patientId, string reportScheduleId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    BadRequest("Parameter facilityId is null or whitespace");
                }

                if (string.IsNullOrWhiteSpace(patientId))
                {
                    BadRequest("Parameter patientId is null or whitespace");
                }

                if (string.IsNullOrWhiteSpace(reportScheduleId))
                {
                    BadRequest("Parameter reportScheduleId is null or whitespace");
                }

                var submissions = (await _submissionEntryManager.FindAsync(e =>
                    e.FacilityId == facilityId && e.PatientId == patientId && e.ReportScheduleId == reportScheduleId)).Select(s => s.PatientSubmission).ToList();

                if (submissions?.Any() ?? false)
                {
                    return Ok(submissions);
                }

                return NotFound();
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ReportController.GetSubmissionBundleForPatient for PatientId {patientId}: {ex.Message}");
                return Problem(ex.Message, statusCode: 500);
            }
        }
    }
}