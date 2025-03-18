using System.Net;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;

        public ReportController(ILogger<ReportController> logger, PatientReportSubmissionBundler patientReportSubmissionBundler, IDatabase database, ScheduledReportFactory scheduledReportFactory)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
        }

        /// <summary>
        /// Returns a serialized PatientSubmissionModel containing all of the Patient level resources and Other resources
        /// for all measure reports for the provided FacilityId, PatientId, and Reporting Period.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="patientId"></param>
        /// <param name="reportScheduleId"></param>
        [HttpGet("Bundle/Patient")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PatientSubmissionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PatientSubmissionModel>> GetSubmissionBundleForPatient(string facilityId, string patientId, string reportScheduleId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    return BadRequest("Parameter facilityId is null or whitespace");
                }

                if (string.IsNullOrWhiteSpace(patientId))
                {
                    return BadRequest("Parameter patientId is null or whitespace");
                }

                if (string.IsNullOrWhiteSpace(reportScheduleId))
                {
                    return BadRequest("Parameter reportScheduleId is null or whitespace");
                }

                var submission = await _patientReportSubmissionBundler.GenerateBundle(facilityId, patientId, reportScheduleId);

                return Ok(submission);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetSubmissionBundleForPatient for facility '{FacilityId}' and patient '{PatientId}'", HtmlInputSanitizer.SanitizeAndRemove(facilityId), HtmlInputSanitizer.Sanitize(patientId));
                return Problem(ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Returns a summary of a ReportSchedule based on the provided facilityId and reportScheduleId
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportScheduleId"></param>
        /// <returns></returns>
        [HttpGet("Schedule")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReportScheduleSummaryModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportScheduleSummaryModel>> GetReportScheduleSummary(string facilityId, string reportScheduleId)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest("Parameter facilityId is null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(reportScheduleId))
            {
                return BadRequest("Parameter reportScheduleId is null or whitespace");
            }

            try
            {

                var model = (await _database.ReportScheduledRepository.FindAsync(r => r.FacilityId == facilityId && r.Id == reportScheduleId)).SingleOrDefault();

                if (model == null)
                {
                    return Problem(detail: "No Report Schedule found for the provided FacilityId and ReportId", statusCode: (int)HttpStatusCode.NotFound);
                }

                return Ok(new ReportScheduleSummaryModel
                {
                    FacilityId = facilityId,
                    ReportId = reportScheduleId,
                    StartDate = model.ReportStartDate,
                    EndDate = model.ReportEndDate,
                    SubmitReportDateTime = model.SubmitReportDateTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportScheduleSummary for facility '{FacilityId}' and report '{ReportId}'", HtmlInputSanitizer.SanitizeAndRemove(facilityId), HtmlInputSanitizer.Sanitize(reportScheduleId));
                return Problem("An error occurred while retrieving the report schedule.", statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        
        /// <summary>
        /// Returns a summary list item of a ReportSchedule based on the provided search criteria
        /// </summary>
        /// <returns></returns>
        [HttpGet("summaries")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ScheduledReportListSummary))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReportScheduleSummaryModel>> GetReportSummaryList()
        {
           //TODO: Add search criteria when requirements have been determined

            try
            {
                var searchResults = await _database.ReportScheduledRepository.SearchAsync(
                    x => true, 
                    sortBy: "CreateDate",
                    sortOrder: SortOrder.Descending, 
                    pageSize: 10, pageNumber: 1, HttpContext.RequestAborted);
                
                var summaries = searchResults.Item1.Select(_scheduledReportFactory.FromDomain).ToList();

                return Ok(new PagedConfigModel<ScheduledReportListSummary>(summaries, searchResults.Item2));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportSummaryList");
                return Problem("An error occurred while retrieving the report summary list.", statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
    }
}
