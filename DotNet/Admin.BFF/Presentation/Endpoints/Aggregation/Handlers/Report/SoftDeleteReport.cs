using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class SoftDeleteReport
{
    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        DataAcquisitionService dataAcquisitionService,
        string reportScheduleId)
    {
        var logger = loggerFactory.CreateLogger("SoftDeleteReport");

        if (string.IsNullOrWhiteSpace(reportScheduleId))
            return Results.BadRequest("Report schedule ID is required.");

        // Pre-check: fetch the report to verify it exists and is not currently running
        HttpResponseMessage getResponse;
        try
        {
            getResponse = await reportService.GetReportScheduleById(context.User, context.RequestAborted, reportScheduleId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching report schedule {ReportScheduleId} for pre-check", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!getResponse.IsSuccessStatusCode)
        {
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return Results.Problem($"Report schedule '{reportScheduleId}' not found.", statusCode: StatusCodes.Status404NotFound);

            return Results.Problem(statusCode: (int)getResponse.StatusCode);
        }

        try
        {
            var body = await getResponse.Content.ReadAsStringAsync(context.RequestAborted);
            var schedule = JsonSerializer.Deserialize<JsonElement>(body);
            if (schedule.TryGetProperty("status", out var statusEl) || schedule.TryGetProperty("Status", out statusEl))
            {
                var statusStr = statusEl.GetString();
                if (string.Equals(statusStr, nameof(ScheduleStatus.New), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(statusStr, nameof(ScheduleStatus.EndOfPeriod), StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Soft-delete blocked for report schedule {ReportScheduleId}: status is {Status}", reportScheduleId.SanitizeForLog(), statusStr.SanitizeForLog());
                    return Results.Problem(
                        $"Report schedule '{reportScheduleId}' cannot be deleted because it is currently in progress (status: {statusStr}).",
                        statusCode: StatusCodes.Status409Conflict);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception parsing report schedule status for {ReportScheduleId}", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        // Step 1: Soft-delete the report schedule
        HttpResponseMessage reportResponse;
        try
        {
            reportResponse = await reportService.SoftDeleteReportScheduleAsync(context.User, reportScheduleId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting report schedule {ReportScheduleId}", reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!reportResponse.IsSuccessStatusCode)
        {
            var detail = await ReadDetailAsync(reportResponse);
            logger.LogWarning("Report schedule soft-delete failed for {ReportScheduleId} with status {StatusCode}", reportScheduleId.SanitizeForLog(), reportResponse.StatusCode);
            return ProblemDetailsExtension.UserFacingProblem(detail ?? "Failed to soft-delete report schedule.", (int)reportResponse.StatusCode);
        }

        // Step 2: Soft-delete acquisition logs for this report — roll back step 1 if this fails
        HttpResponseMessage daResponse;
        try
        {
            daResponse = await dataAcquisitionService.SoftDeleteLogsByReportTrackingIdAsync(context.User, reportScheduleId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting acquisition logs for report {ReportScheduleId} — rolling back step 1", reportScheduleId.SanitizeForLog());
            await RollbackReportScheduleAsync(reportService, context, reportScheduleId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete acquisition logs. Report soft-delete has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!daResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Acquisition log soft-delete failed for report {ReportScheduleId} with status {StatusCode} — rolling back step 1", reportScheduleId.SanitizeForLog(), daResponse.StatusCode);
            await RollbackReportScheduleAsync(reportService, context, reportScheduleId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete acquisition logs. Report soft-delete has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("Report schedule {ReportScheduleId} and its acquisition logs were successfully soft deleted", reportScheduleId.SanitizeForLog());
        return Results.NoContent();
    }

    private static async Task RollbackReportScheduleAsync(ReportService reportService, HttpContext context, string reportScheduleId, ILogger logger)
    {
        try
        {
            var response = await reportService.RestoreReportScheduleAsync(context.User, reportScheduleId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not restore report schedule {ReportScheduleId} (status {StatusCode})", reportScheduleId.SanitizeForLog(), response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception restoring report schedule {ReportScheduleId}", reportScheduleId.SanitizeForLog());
        }
    }

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
