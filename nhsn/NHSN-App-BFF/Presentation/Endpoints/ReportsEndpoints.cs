using System.Globalization;
using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The Generate Test Report step. Requesting a report is a Tenant write, so the id comes back
// synchronously; the report itself is watched from the Report Results step, which reads its
// status off the onboarding draft rather than a dedicated endpoint here.
public class ReportsEndpoints : IApi
{
    private const int MaxPatientIds = 10;

    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/reports")
            .WithTags("Reports")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPost("/", async (
                ReportRequest request,
                IReportingService service,
                CancellationToken cancellationToken) =>
            {
                var errors = Validate(request);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                return Results.Ok(await service.RequestReportAsync(request, cancellationToken));
            })
            .WithName("RequestReport")
            .Produces<ReportSummary>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Request an ad hoc test report.";
                operation.Description =
                    "Answers with the requested report and a Pending status. The id is assigned " +
                    "synchronously and the report is generated behind it, so the status here is " +
                    "always Pending -- read the report list or detail for progress. A report " +
                    "already in flight does not refuse a second one; Link only reports a schedule " +
                    "complete once it has been submitted, and these bypass submission, so " +
                    "\"pending\" never clears and refusing on it would block every report after " +
                    "the first. Requested reports bypass submission: this is a test report for a " +
                    "facility that is not enrolled yet.";
                return operation;
            });

        group.MapGet("/", async (
                IReportingService service,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 10) =>
                Results.Ok(await service.ListReportsAsync(page < 1 ? 1 : page, pageSize < 1 ? 10 : pageSize, cancellationToken)))
            .WithName("ListReports")
            .Produces<Paged<ReportSummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Lists the current facility's reports, newest first.";
                return operation;
            });

        group.MapGet("/{reportId}", async (
                string reportId,
                IReportingService service,
                CancellationToken cancellationToken) =>
            {
                var detail = await service.GetReportAsync(reportId, cancellationToken);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .WithName("GetReport")
            .Produces<ReportDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Reads one report's full detail.";
                return operation;
            });

        group.MapGet("/{reportId}/patients", async (
                string reportId,
                IReportingService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetReportPatientsAsync(reportId, cancellationToken)))
            .WithName("GetReportPatients")
            .Produces<List<ReportPatientEntry>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Reads the per-patient rows behind the Report Details patient table.";
                return operation;
            });

        group.MapGet("/{reportId}/patients/{patientId}/mapping-evidence", async (
                string reportId,
                string patientId,
                IReportingService service,
                CancellationToken cancellationToken) =>
            {
                var evidence = await service.GetPatientMappingEvidenceAsync(reportId, patientId, cancellationToken);
                return evidence is null ? Results.NotFound() : Results.Ok(evidence);
            })
            .WithName("GetPatientMappingEvidence")
            .Produces<PatientMappingEvidence>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Reads the real evidence behind one patient's Location Org / HSLOC / Encounter mapping indicators.";
                return operation;
            });

        group.MapGet("/{reportId}/query-plan", async (
                string reportId,
                IReportingService service,
                CancellationToken cancellationToken) =>
            {
                var plan = await service.GetQueryPlanAsync(reportId, cancellationToken);
                return plan is null ? Results.NotFound() : Results.Ok(plan);
            })
            .WithName("GetQueryPlan")
            .Produces<QueryPlan>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Reads the facility's configured DataAcquisition query plan for this report's vendor.";
                return operation;
            });

        group.MapGet("/{reportId}/acquisition-logs", async (
                string reportId,
                IReportingService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAcquisitionLogsAsync(reportId, cancellationToken)))
            .WithName("GetAcquisitionLogs")
            .Produces<List<AcquisitionLogEntry>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Reads DataAcquisition's acquisition log entries recorded for this report.";
                return operation;
            });

        group.MapGet("/{reportId}/summary-export", async (
                string reportId,
                IReportingService service,
                CancellationToken cancellationToken) =>
            {
                var summary = await service.GetAcquisitionSummaryAsync(reportId, cancellationToken);
                if (summary is null)
                {
                    return Results.NotFound();
                }
                var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions {WriteIndented = true});
                return Results.File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"report-{reportId}-summary.json");
            })
            .WithName("ExportReportSummary")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Downloads DataAcquisition's summary counts for this report as a JSON file.";
                return operation;
            });

        group.MapGet("/{reportId}/patients/{patientId}/export", async (
                string reportId,
                string patientId,
                string measure,
                IReportingService service,
                INhsnUserContext userContext,
                CancellationToken cancellationToken) =>
            {
                var export = await service.GetPatientMeasureReportExportAsync(reportId, patientId, measure, cancellationToken);
                if (export is null)
                {
                    return Results.NotFound();
                }

                var measureReportResource = new
                {
                    resourceType = "MeasureReport",
                    id = export.MeasureReportId ?? $"{reportId}-{patientId}-{measure}",
                    status = "complete",
                    type = "individual",
                    measure = export.ReportType,
                    date = DateTime.UtcNow.ToString("O"),
                    reporter = userContext.FacilityName is null
                        ? null
                        : new {display = userContext.FacilityName},
                    period = export.PeriodStart is null || export.PeriodEnd is null
                        ? null
                        : new
                        {
                            start = export.PeriodStart.Value.ToString("yyyy-MM-dd"),
                            end = export.PeriodEnd.Value.ToString("yyyy-MM-dd")
                        },
                    subject = new {reference = $"Patient/{patientId}"},
                    evaluatedResource = export.EvaluatedResources
                        .Select(resource => new {reference = $"{resource.ResourceType}/{resource.ResourceId}"})
                        .ToArray(),
                    extension = new[]
                    {
                        new {url = "urn:nhsn-link:reportingStatus", valueString = export.ReportingStatus}
                    }
                };

                var ndjson = JsonSerializer.Serialize(measureReportResource);
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(ndjson),
                    "application/x-ndjson",
                    $"{patientId}_{measure}_report.ndjson");
            })
            .WithName("ExportPatientReport")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Downloads one patient's MeasureReport for a report type as an ndjson file.";
                operation.Description =
                    "Carries the MeasureReport resource and its evaluated-resource references only -- " +
                    "Report exposes no operation that returns the underlying clinical resources' " +
                    "content, so the Patient/Encounter/MedicationRequest bodies those references " +
                    "point to are not included.";
                return operation;
            });
    }

    private static Dictionary<string, string[]> Validate(ReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Measures.Count == 0 || request.Measures.All(string.IsNullOrWhiteSpace))
        {
            errors[nameof(ReportRequest.Measures)] = ["At least one measure is required."];
        }

        var start = ParseDate(request.StartDate);
        if (start is null)
        {
            errors[nameof(ReportRequest.StartDate)] = ["A start date in yyyy-MM-dd format is required."];
        }

        var end = ParseDate(request.EndDate);
        if (end is null)
        {
            errors[nameof(ReportRequest.EndDate)] = ["An end date in yyyy-MM-dd format is required."];
        }
        else if (start is not null && end < start)
        {
            errors[nameof(ReportRequest.EndDate)] = ["The end date cannot precede the start date."];
        }

        if (request.PatientIds.Count > MaxPatientIds)
        {
            errors[nameof(ReportRequest.PatientIds)] =
                [$"No more than {MaxPatientIds} FHIR patient ids may be reported on at once."];
        }

        return errors;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
