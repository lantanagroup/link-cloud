using System.Globalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
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
