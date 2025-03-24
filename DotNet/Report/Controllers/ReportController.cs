using System.Linq.Expressions;
using System.Net;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
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
        private readonly ISubmissionEntryManager _submissionEntryManager;

        public ReportController(ILogger<ReportController> logger, PatientReportSubmissionBundler patientReportSubmissionBundler, IDatabase database, ISubmissionEntryManager submissionEntryManager)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _database = database;
            _submissionEntryManager = submissionEntryManager;
        }

        /// <summary>
        /// Returns a serialized PatientSubmissionModel containing all the Patient level resources and Other resources
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
        /// <param name="facilityId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet("summaries")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ScheduledReportListSummary>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ScheduledReportListSummary>>> GetReportSummaryList(
            string? facilityId, int pageNumber = 1, int pageSize = 10)
        {
            //TODO: Add search criteria when requirements have been determined

            if (pageNumber < 1)
            {
                return BadRequest("Parameter pageNumber must be greater than 0");
            }

            if (pageSize < 1)
            {
                return BadRequest("Parameter pageSize must be greater than 0");
            }

            try
            {
                // Create search predicates
                //TODO: design way to dynamically build predicates or change search to use custom method
                Expression<Func<ReportScheduleModel, bool>> predicate;
                if (facilityId is null)
                {
                    predicate = r => true;
                }
                else
                {

                    predicate = r => r.FacilityId == facilityId;
                }

                var summaries =
                    await _submissionEntryManager.GetScheduledReportSummaries(predicate, pageSize, pageNumber,  HttpContext.RequestAborted);

                return Ok(summaries);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportSummaryList");
                return Problem("An error occurred while retrieving the report summary list.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns a summary of a scheduled report based on the provided reportId
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ScheduledReportListSummary))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ScheduledReportListSummary>> GetReportSummary(string facilityId, string reportId)
        {
           //TODO: Add search criteria when requirements have been determined

            if (string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("Parameter facility cannot be null or empty");
            }
            
            if (string.IsNullOrEmpty(reportId))
            {
                return BadRequest("A report id needs to be specified");
            }
            
            try
            {
                var summary =
                    await _submissionEntryManager.GetScheduledReportSummary(facilityId, reportId, HttpContext.RequestAborted);
                
                return Ok(summary);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportSummary");
                return Problem("An error occurred while retrieving the report summary.", statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns a summary list item of a ReportSchedule based on the provided search criteria
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}/measure-reports")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<MeasureReportSummary>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<MeasureReportSummary>>> GetMeasureReports(
            string facilityId, string? reportId, int pageNumber = 1, int pageSize = 10)
        {
            //TODO: Add search criteria when requirements have been determined

            if (pageNumber < 1)
            {
                return BadRequest("Parameter pageNumber must be greater than 0");
            }

            if (pageSize < 1)
            {
                return BadRequest("Parameter pageSize must be greater than 0");
            }
            
            if(string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("Parameter facilityId cannot be null or empty");
            }

            try
            {
                // Create search predicates
                //TODO: design way to dynamically build predicates or change search to use custom method
                Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate;
                if (reportId is null)
                {
                    predicate = r => r.FacilityId == facilityId;
                }
                else
                {

                    predicate = r => r.FacilityId == facilityId && r.ReportScheduleId == reportId;
                }

                var summaries =
                    await _submissionEntryManager.GetMeasureReports(predicate, pageSize, pageNumber, HttpContext.RequestAborted);

                return Ok(summaries);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetMeasureReports");
                return Problem("An error occurred while retrieving measures reports.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        
        /// <summary>
        /// Returns a summary list item of a ReportSchedule based on the provided search criteria
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}/measure-reports/{reportId}/resources")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ResourceSummary>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ResourceSummary>>> GetMeasureReportResources(
            string facilityId, string reportId, int pageNumber = 1, int pageSize = 10)
        {
            //TODO: Add search criteria when requirements have been determined

            if (pageNumber < 1)
            {
                return BadRequest("Parameter pageNumber must be greater than 0");
            }

            if (pageSize < 1)
            {
                return BadRequest("Parameter pageSize must be greater than 0");
            }
            
            if(string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("Parameter facilityId cannot be null or empty");
            }

            try
            {
                // Create search predicates
                //TODO: design way to dynamically build predicates or change search to use custom method
                Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate = r => r.FacilityId == facilityId && r.ReportScheduleId == reportId;
              
                var resources =
                    await _submissionEntryManager.GetMeasureReportResourceSummary(facilityId, reportId, pageSize, pageNumber, HttpContext.RequestAborted);

                return Ok(resources);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetMeasureReports");
                return Problem("An error occurred while retrieving measures reports.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
    }
}
