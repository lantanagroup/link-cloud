using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class RestoreReport
{
    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        DataAcquisitionService dataAcquisitionService,
        string reportScheduleId)
    {
        var logger = loggerFactory.CreateLogger("RestoreReport");

        if (string.IsNullOrWhiteSpace(reportScheduleId))
            return Results.BadRequest("Report schedule ID is required.");

        // Step 1: Restore the report schedule
        HttpResponseMessage reportResponse;
        try
        {
            reportResponse = await reportService.RestoreReportScheduleAsync(context.User, reportScheduleId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring report schedule {ReportScheduleId}", reportScheduleId);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!reportResponse.IsSuccessStatusCode)
        {
            var detail = await ReadDetailAsync(reportResponse);
            logger.LogWarning("Report schedule restore failed for {ReportScheduleId} with status {StatusCode}", reportScheduleId, reportResponse.StatusCode);
            return ProblemDetailsExtension.UserFacingProblem(detail ?? "Failed to restore report schedule.", (int)reportResponse.StatusCode);
        }

        // Step 2: Restore acquisition logs — roll back step 1 if this fails
        HttpResponseMessage daResponse;
        try
        {
            daResponse = await dataAcquisitionService.RestoreLogsByReportTrackingIdAsync(context.User, reportScheduleId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring acquisition logs for report {ReportScheduleId} — rolling back step 1", reportScheduleId);
            await RollbackReportScheduleAsync(reportService, context, reportScheduleId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore acquisition logs. Report restore has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!daResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Acquisition log restore failed for report {ReportScheduleId} with status {StatusCode} — rolling back step 1", reportScheduleId, daResponse.StatusCode);
            await RollbackReportScheduleAsync(reportService, context, reportScheduleId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore acquisition logs. Report restore has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("Report schedule {ReportScheduleId} and its acquisition logs were successfully restored", reportScheduleId);
        return Results.NoContent();
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

    private static async Task RollbackReportScheduleAsync(ReportService reportService, HttpContext context, string reportScheduleId, ILogger logger)
    {
        try
        {
            var response = await reportService.SoftDeleteReportScheduleAsync(context.User, reportScheduleId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not re-delete report schedule {ReportScheduleId} (status {StatusCode})", reportScheduleId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception re-deleting report schedule {ReportScheduleId}", reportScheduleId);
        }
    }
}
