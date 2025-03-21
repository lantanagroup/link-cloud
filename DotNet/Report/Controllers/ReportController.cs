using System.Linq.Expressions;
using System.Net;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
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
        private readonly ISubmissionEntryManager _submissionEntryManager;

        public ReportController(ILogger<ReportController> logger, PatientReportSubmissionBundler patientReportSubmissionBundler, IDatabase database, ScheduledReportFactory scheduledReportFactory, ISubmissionEntryManager submissionEntryManager)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
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
        public async Task<ActionResult<PagedConfigModel<ScheduledReportListSummary>>> GetReportSummaryList(string? facilityId, int pageNumber = 1, int pageSize = 10)
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
                
                var searchResults = await _database.ReportScheduledRepository.SearchAsync(
                    predicate, 
                    sortBy: "CreateDate",
                    sortOrder: SortOrder.Descending, 
                    pageSize: pageSize, pageNumber: pageNumber, HttpContext.RequestAborted);
                
                var summaries = searchResults.Item1.Select(_scheduledReportFactory.FromDomain).ToList();

                // Get total IP count for reports in list
                var populationCounts = await _submissionEntryManager
                    .GetReportInitialPopulationCountBatch(summaries.Select(x => x.Id)
                        .Distinct().ToList(), HttpContext.RequestAborted);
                
                foreach (var summary in summaries)
                {
                    if(!populationCounts.TryGetValue(summary.Id, out var count)) continue;
                    
                    summary.InitialPopulationCount = count;
                }

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
