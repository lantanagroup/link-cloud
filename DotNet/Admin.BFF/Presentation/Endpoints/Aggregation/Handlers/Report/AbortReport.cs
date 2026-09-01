using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class AbortReport
{
    private static readonly TimeSpan AbortTtl = TimeSpan.FromDays(14);

    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        DataAcquisitionService dataAcquisitionService,
        IPipelineAbortRegistry abortRegistry,
        string reportScheduleId)
    {
        var logger = loggerFactory.CreateLogger("AbortReport");

        if (string.IsNullOrWhiteSpace(reportScheduleId))
            return Results.BadRequest("Report schedule ID is required.");

        HttpResponseMessage getResponse;
        try
        {
            getResponse = await reportService.GetReportScheduleById(context.User, context.RequestAborted, reportScheduleId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching report schedule {ReportScheduleId} for abort", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!getResponse.IsSuccessStatusCode)
        {
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return Results.Problem($"Report schedule '{reportScheduleId}' not found.", statusCode: StatusCodes.Status404NotFound);

            return Results.Problem(statusCode: (int)getResponse.StatusCode);
        }

        string? statusStr = null;
        try
        {
            var body = await getResponse.Content.ReadAsStringAsync(context.RequestAborted);
            var schedule = JsonSerializer.Deserialize<JsonElement>(body);
            if (schedule.TryGetProperty("status", out var statusEl) || schedule.TryGetProperty("Status", out statusEl))
                statusStr = statusEl.GetString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception parsing report schedule status for {ReportScheduleId}", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!IsInProgress(statusStr))
        {
            return Results.Problem(
                $"Report schedule '{reportScheduleId}' is not in progress (status: {statusStr ?? "unknown"}). Use soft delete for completed or scheduled reports.",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            await abortRegistry.AbortAsync(facilityId: null, reportScheduleId, AbortTtl, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write pipeline abort flag for report {ReportScheduleId}", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        try
        {
            var cancelResponse = await dataAcquisitionService.CancelLogsByFilterAsync(
                context.User,
                new { ReportId = reportScheduleId },
                minAgeHours: 0,
                context.RequestAborted);
            if (!cancelResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "DA cancel-by-filter returned {StatusCode} for aborted report {ReportScheduleId}",
                    cancelResponse.StatusCode, reportScheduleId.SanitizeForLog());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DA cancel-by-filter failed for aborted report {ReportScheduleId}", reportScheduleId.SanitizeForLog());
        }

        HttpResponseMessage reportResponse;
        try
        {
            reportResponse = await reportService.SoftDeleteReportScheduleAsync(context.User, reportScheduleId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting aborted report schedule {ReportScheduleId}", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!reportResponse.IsSuccessStatusCode)
        {
            var detail = await ReadDetailAsync(reportResponse);
            logger.LogWarning("Report schedule soft-delete failed after abort for {ReportScheduleId} with status {StatusCode}", reportScheduleId.SanitizeForLog(), reportResponse.StatusCode);
            return ProblemDetailsExtension.UserFacingProblem(detail ?? "Failed to soft-delete aborted report schedule.", (int)reportResponse.StatusCode);
        }

        try
        {
            var daResponse = await dataAcquisitionService.SoftDeleteLogsByReportTrackingIdAsync(context.User, reportScheduleId, context.RequestAborted);
            if (!daResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Acquisition log soft-delete returned {StatusCode} for aborted report {ReportScheduleId}",
                    daResponse.StatusCode, reportScheduleId.SanitizeForLog());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Acquisition log soft-delete failed for aborted report {ReportScheduleId}", reportScheduleId.SanitizeForLog());
        }

        logger.LogInformation("Aborted in-progress report {ReportScheduleId} and stopped its acquisition work.", reportScheduleId.SanitizeForLog());
        return Results.NoContent();
    }

    private static bool IsInProgress(string? status) =>
        string.Equals(status, nameof(ScheduleStatus.New), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, nameof(ScheduleStatus.EndOfPeriod), StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            return json.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
