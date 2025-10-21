using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Facility;

public static class SoftDeleteFacility
{
    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        TenantService tenantService,
        ReportService reportService,
        DataAcquisitionService dataAcquisitionService,
        CensusService censusService,
        string facilityId)
    {
        var logger = loggerFactory.CreateLogger("SoftDeleteFacility");

        // Pre-check: block if the facility has reports currently running (New or EndOfPeriod)
        try
        {
            var activeResponse = await reportService.GetActiveReportSchedulesAsync(context.User, facilityId, context.RequestAborted);
            if (activeResponse.IsSuccessStatusCode)
            {
                var body = await activeResponse.Content.ReadAsStringAsync(context.RequestAborted);
                var schedules = JsonSerializer.Deserialize<JsonElement>(body);
                if (schedules.ValueKind == JsonValueKind.Array && schedules.GetArrayLength() > 0)
                {
                    logger.LogWarning("Soft-delete blocked for facility {FacilityId}: {Count} report(s) currently running", facilityId.SanitizeForLog(), schedules.GetArrayLength());
                    return Results.Problem(
                        $"This tenant cannot be soft-deleted because there are {schedules.GetArrayLength()} report(s) currently in progress. Please wait for all reports to complete before trying again.",
                        statusCode: StatusCodes.Status409Conflict);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception checking active report schedules for facility {FacilityId}", facilityId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        // Step 1: Soft-delete the tenant — gating operation, no roll back needed if this fails
        HttpResponseMessage tenantResponse;
        try
        {
            tenantResponse = await tenantService.SoftDeleteFacilityAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting tenant for facility {FacilityId}", facilityId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!tenantResponse.IsSuccessStatusCode)
        {
            var tenantDetail = await ReadDetailAsync(tenantResponse);
            logger.LogWarning("Tenant soft-delete failed for facility {FacilityId} with status {StatusCode}", facilityId.SanitizeForLog(), tenantResponse.StatusCode);
            return ProblemDetailsExtension.UserFacingProblem(tenantDetail ?? "Failed to soft-delete tenant.", (int)tenantResponse.StatusCode);
        }

        // Step 2: Soft-delete report schedules — roll back step 1 if this fails
        HttpResponseMessage reportResponse;
        try
        {
            reportResponse = await reportService.SoftDeleteReportSchedulesAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting report schedules for facility {FacilityId} — rolling back step 1", facilityId.SanitizeForLog());
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete report schedules. Tenant soft-delete has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!reportResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Report schedule soft-delete failed for facility {FacilityId} with status {StatusCode} — rolling back step 1", facilityId.SanitizeForLog(), reportResponse.StatusCode);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete report schedules. Tenant soft-delete has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        // Step 3: Soft-delete DA logs — roll back steps 1 and 2 if this fails
        HttpResponseMessage daResponse;
        try
        {
            daResponse = await dataAcquisitionService.SoftDeleteLogsAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception soft-deleting acquisition logs for facility {FacilityId} — rolling back steps 1 and 2", facilityId.SanitizeForLog());
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete acquisition logs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!daResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Acquisition log soft-delete failed for facility {FacilityId} with status {StatusCode} — rolling back steps 1 and 2", facilityId.SanitizeForLog(), daResponse.StatusCode);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to soft-delete acquisition logs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        // Step 4: Delete Census cron jobs — roll back steps 1, 2 and 3 if this fails
        HttpResponseMessage censusResponse;
        try
        {
            censusResponse = await censusService.DeleteCensusJobsAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception deleting census jobs for facility {FacilityId} — rolling back steps 1, 2 and 3", facilityId.SanitizeForLog());
            await RollbackAcquisitionLogsAsync(dataAcquisitionService, context, facilityId, logger);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to delete census jobs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!censusResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Census job deletion failed for facility {FacilityId} with status {StatusCode} — rolling back steps 1, 2 and 3", facilityId.SanitizeForLog(), censusResponse.StatusCode);
            await RollbackAcquisitionLogsAsync(dataAcquisitionService, context, facilityId, logger);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to delete census jobs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new FacilitySoftDeleteResult
        {
            FacilityId = facilityId,
            TenantDeleted = true,
            ReportSchedulesDeleted = true,
            AcquisitionLogsDeleted = true,
            CensusJobsDeleted = true
        });
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

    private static async Task RollbackTenantAsync(TenantService tenantService, HttpContext context, string facilityId, ILogger logger)
    {
        try
        {
            var response = await tenantService.RestoreFacilityAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not restore tenant for facility {FacilityId} (status {StatusCode})", facilityId.SanitizeForLog(), response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception restoring tenant for facility {FacilityId}", facilityId.SanitizeForLog());
        }
    }

    private static async Task RollbackReportSchedulesAsync(ReportService reportService, HttpContext context, string facilityId, ILogger logger)
    {
        try
        {
            var response = await reportService.RestoreReportSchedulesAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not restore report schedules for facility {FacilityId} (status {StatusCode})", facilityId.SanitizeForLog(), response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception restoring report schedules for facility {FacilityId}", facilityId.SanitizeForLog());
        }
    }

    private static async Task RollbackAcquisitionLogsAsync(DataAcquisitionService dataAcquisitionService, HttpContext context, string facilityId, ILogger logger)
    {
        try
        {
            var response = await dataAcquisitionService.RestoreLogsAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not restore acquisition logs for facility {FacilityId} (status {StatusCode})", facilityId.SanitizeForLog(), response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception restoring acquisition logs for facility {FacilityId}", facilityId.SanitizeForLog());
        }
    }
}
