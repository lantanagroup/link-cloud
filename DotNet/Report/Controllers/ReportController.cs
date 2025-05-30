using System.Linq.Expressions;
using System.Net;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using LinqKit;
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
        private readonly IReportScheduledManager _reportingScheduledManager;
        public ReportController(ILogger<ReportController> logger, PatientReportSubmissionBundler patientReportSubmissionBundler, IDatabase database, ISubmissionEntryManager submissionEntryManager, IReportScheduledManager reportingScheduledManager)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _database = database;
            _submissionEntryManager = submissionEntryManager;
            _reportingScheduledManager = reportingScheduledManager;
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
                    await _reportingScheduledManager.GetScheduledReportSummaries(predicate, "CreateDate", SortOrder.Descending, pageSize, pageNumber,  HttpContext.RequestAborted);

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
                    await _reportingScheduledManager.GetScheduledReportSummary(facilityId, reportId, HttpContext.RequestAborted);
                
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
        /// <param name="sortOrder"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <param name="patientId"></param>
        /// <param name="measureReportId"></param>
        /// <param name="measure"></param>
        /// <param name="reportStatus"></param>
        /// <param name="validationStatus"></param>
        /// <param name="sortBy"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}/measure-reports")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<MeasureReportSummary>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<MeasureReportSummary>>> GetMeasureReports(
            string facilityId, string? reportId, string? patientId, string? measureReportId, string? measure, 
            PatientSubmissionStatus? reportStatus, ValidationStatus? validationStatus, string? sortBy, 
            SortOrder sortOrder = SortOrder.Descending, int pageNumber = 1, int pageSize = 10)
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
                Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate = r => r.FacilityId == facilityId;

                if (!string.IsNullOrEmpty(reportId))
                {
                    predicate = predicate.And(r => r.ReportScheduleId == reportId);
                }

                if (!string.IsNullOrEmpty(patientId))
                {
                    predicate = predicate.And(r => r.PatientId == patientId);
                }
                
                if (!string.IsNullOrEmpty(measureReportId))
                {
                    predicate = predicate.And(r => r.Id == measureReportId);
                }

                if (!string.IsNullOrEmpty(measure))
                {
                    predicate = predicate.And(r => r.ReportType == measure);
                }

                if (reportStatus is not null)
                {
                    predicate = predicate.And(r => r.Status == reportStatus);
                }

                if (validationStatus is not null)
                {
                    predicate = predicate.And(r => r.ValidationStatus == validationStatus);
                }
                
                if (string.IsNullOrEmpty(sortBy))
                {
                    sortBy = "CreateDate";
                }

                var sortCol = sortBy.ToLowerInvariant() switch
                {
                    "createdate" => "CreateDate",
                    "patientid" => "PatientId",
                    "reporttype" => "ReportType",
                    "status" => "Status",
                    "validationstatus" => "ValidationStatus",
                    _ => "CreateDate"
                };
                 

                var summaries =
                    await _submissionEntryManager.GetMeasureReports(predicate, sortCol, sortOrder, pageSize, pageNumber, HttpContext.RequestAborted);

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
        /// <param name="resourceType"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}/measure-reports/{reportId}/resources")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<ResourceSummary>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<ResourceSummary>>> GetMeasureReportResources(
            string facilityId, string reportId, ResourceType? resourceType, int pageNumber = 1, int pageSize = 10)
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
                    await _submissionEntryManager.GetMeasureReportResourceSummary(facilityId, reportId, resourceType, pageSize, pageNumber, HttpContext.RequestAborted);

                return Ok(resources);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetMeasureReports");
                return Problem("An error occurred while retrieving measures reports.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        
        /// <summary>
        /// Returns a list of unique resouces types contained in a measure report
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <returns></returns>
        [HttpGet("summaries/{facilityId}/measure-reports/{reportId}/resource-types")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<string>>> GetMeasureReportResourceTypes(
            string facilityId, string reportId)
        {
            if(string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("Parameter facilityId cannot be null or empty");
            }
            
            if(string.IsNullOrEmpty(reportId))
            {
                return BadRequest("Parameter reportId cannot be null or empty");
            }

            try
            {
                var resourceTypes = await
                    _submissionEntryManager.GetMeasureReportResourceTypeList(facilityId, reportId,
                        HttpContext.RequestAborted);

                return Ok(resourceTypes);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetMeasureReportResourceTypes");
                return Problem("An error occurred while retrieving resource types within a measure report.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        
        /// <summary>
        /// Returns a list of possible report submission statuses
        /// </summary>
        /// <returns></returns>
        [HttpGet("report-submission-statuses")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetReportSubmissionStatuses()
        {
            try
            {
                var submissionStatuses = Enum.GetNames(typeof(PatientSubmissionStatus)).ToList();

                return Ok(submissionStatuses);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportSubmissionStatuses");
                return Problem("An error occurred while retrieving submission statuses.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }
        
        /// <summary>
        /// Returns a list of possible report validation statuses
        /// </summary>
        /// <returns></returns>
        [HttpGet("report-validation-statuses")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetReportValidationStatuses()
        {
            try
            {
                var submissionStatuses = Enum.GetNames(typeof(ValidationStatus)).ToList();

                return Ok(submissionStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetReportValidationStatuses");
                return Problem("An error occurred while retrieving validation statuses.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns a list of unique resouces types contained in a measure report
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <param name="page"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [HttpGet("{facilityId}/{reportId}/patient")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedConfigModel<PatientSummary>>> GetPatients(string facilityId, string reportId, int page = 1, int count = 10)
        {
            if (page < 1)
            {
                return BadRequest("Parameter pageNumber must be greater than 0");
            }

            if (count < 1)
            {
                return BadRequest("Parameter pageSize must be greater than 0");
            }

            if (string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("Parameter facilityId cannot be null or empty");
            }

            try
            {
                var patients = await _submissionEntryManager.GetPatients(facilityId, reportId, page, count, HttpContext.RequestAborted);

                return Ok(patients);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReportController.GetMeasureReportResourceTypes");
                return Problem("An error occurred while retrieving resource types within a measure report.",
                    statusCode: (int)HttpStatusCode.InternalServerError);
            }
        }

    }
}
