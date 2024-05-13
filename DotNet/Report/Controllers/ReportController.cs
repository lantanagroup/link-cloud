using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly MeasureReportSubmissionBundler _bundler;
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly IMediator _mediator;

        public ReportController(ILogger<ReportController> logger, MeasureReportSubmissionBundler bundler, IMediator mediator, PatientReportSubmissionBundler patientReportSubmissionBundler)
        {
            _logger = logger;
            _bundler = bundler;
            _mediator = mediator;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
        }

        /// <summary>
        /// Generates the bundle for the report indicated by the provided parameters.
        /// </summary>
        /// <param name="reportId"></param>
        /// <returns></returns>
        [HttpGet("GetSubmissionBundle")]
        public async Task<JsonElement> GetSubmissionBundle(string reportId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reportId))
                {
                    BadRequest("Paramater reportId is null or whitespace");
                }

                _logger.LogInformation($"Executing GenerateSubmissionBundleJob for MeasureReportScheduleModel {reportId}");

                MeasureReportSubmissionModel submission = await _bundler.GenerateBundle(reportId);

                return JsonSerializer.SerializeToElement(submission.SubmissionBundle, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ReportController.GetSubmissionBundle for ReportId {reportId}: {ex.Message}");
                throw;
            }
        }

        [HttpGet("GetSubmissionBundleForPatient")]
        public async Task<JsonElement> GetSubmissionBundleForPatient(string facilityId, string patientId, DateTime startDate, DateTime endDate)
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

                return JsonSerializer.SerializeToElement(submission, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ReportController.GetSubmissionBundleForPatient for PatientId {patientId}: {ex.Message}");
                throw;
            }
        }
    }
}