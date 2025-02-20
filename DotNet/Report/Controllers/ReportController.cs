using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;
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
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;

        public ReportController(ILogger<ReportController> logger,PatientReportSubmissionBundler patientReportSubmissionBundler)
        {
            _logger = logger;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MeasureReportSubmissionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MeasureReportSubmissionModel>> GetSubmissionBundleForPatient(string facilityId, string patientId, DateTime startDate, DateTime endDate)
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

                try
                {
                    var submission = await _patientReportSubmissionBundler.GenerateBundle(facilityId, patientId, startDate, endDate);

                    return Ok(submission);
                }
                catch (ArgumentException e)
                {
                    return NotFound(e.Message);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in ReportController.GetSubmissionBundleForPatient");
                return Problem(ex.Message, statusCode: 500);
            }
        }
    }
}