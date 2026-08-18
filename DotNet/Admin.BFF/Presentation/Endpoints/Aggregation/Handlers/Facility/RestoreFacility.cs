using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Facility;

public static class RestoreFacility
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
        var logger = loggerFactory.CreateLogger("RestoreFacility");

        // Step 1: Restore the tenant — no roll back needed if this fails
        HttpResponseMessage tenantResponse;
        try
        {
            tenantResponse = await tenantService.RestoreFacilityAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring tenant for facility {FacilityId}", facilityId);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!tenantResponse.IsSuccessStatusCode)
        {
            var tenantDetail = await ReadDetailAsync(tenantResponse);
            logger.LogWarning("Tenant restore failed for facility {FacilityId} with status {StatusCode}", facilityId, tenantResponse.StatusCode);
            return ProblemDetailsExtension.UserFacingProblem(tenantDetail ?? "Failed to restore tenant.", (int)tenantResponse.StatusCode);
        }

        // Step 2: Restore report schedules — roll back step 1 if this fails
        HttpResponseMessage reportResponse;
        try
        {
            reportResponse = await reportService.RestoreReportSchedulesAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring report schedules for facility {FacilityId} — rolling back step 1", facilityId);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore report schedules. Tenant restore has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!reportResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Report schedule restore failed for facility {FacilityId} with status {StatusCode} — rolling back step 1", facilityId, reportResponse.StatusCode);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore report schedules. Tenant restore has been rolled back.", StatusCodes.Status500InternalServerError);
        }

        // Step 3: Restore DA logs — roll back steps 1 and 2 if this fails
        HttpResponseMessage daResponse;
        try
        {
            daResponse = await dataAcquisitionService.RestoreLogsAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring acquisition logs for facility {FacilityId} — rolling back steps 1 and 2", facilityId);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore acquisition logs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!daResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Acquisition log restore failed for facility {FacilityId} with status {StatusCode} — rolling back steps 1 and 2", facilityId, daResponse.StatusCode);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore acquisition logs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        // Step 4: Restore Census cron jobs — roll back steps 1, 2 and 3 if this fails
        HttpResponseMessage censusResponse;
        try
        {
            censusResponse = await censusService.RestoreCensusJobsAsync(context.User, facilityId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception restoring census jobs for facility {FacilityId} — rolling back steps 1, 2 and 3", facilityId);
            await RollbackAcquisitionLogsAsync(dataAcquisitionService, context, facilityId, logger);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore census jobs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        if (!censusResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Census job restore failed for facility {FacilityId} with status {StatusCode} — rolling back steps 1, 2 and 3", facilityId, censusResponse.StatusCode);
            await RollbackAcquisitionLogsAsync(dataAcquisitionService, context, facilityId, logger);
            await RollbackReportSchedulesAsync(reportService, context, facilityId, logger);
            await RollbackTenantAsync(tenantService, context, facilityId, logger);
            return ProblemDetailsExtension.UserFacingProblem("Failed to restore census jobs. All previous steps have been rolled back.", StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new FacilityRestoreResult
        {
            FacilityId = facilityId,
            TenantRestored = true,
            ReportSchedulesRestored = true,
            AcquisitionLogsRestored = true,
            CensusJobsRestored = true
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
            var response = await tenantService.SoftDeleteFacilityAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not soft-delete tenant for facility {FacilityId} (status {StatusCode})", facilityId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception soft-deleting tenant for facility {FacilityId}", facilityId);
        }
    }

    private static async Task RollbackReportSchedulesAsync(ReportService reportService, HttpContext context, string facilityId, ILogger logger)
    {
        try
        {
            var response = await reportService.SoftDeleteReportSchedulesAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not soft-delete report schedules for facility {FacilityId} (status {StatusCode})", facilityId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception soft-deleting report schedules for facility {FacilityId}", facilityId);
        }
    }

    private static async Task RollbackAcquisitionLogsAsync(DataAcquisitionService dataAcquisitionService, HttpContext context, string facilityId, ILogger logger)
    {
        try
        {
            var response = await dataAcquisitionService.SoftDeleteLogsAsync(context.User, facilityId, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Rollback failed: could not soft-delete acquisition logs for facility {FacilityId} (status {StatusCode})", facilityId, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rollback failed: exception soft-deleting acquisition logs for facility {FacilityId}", facilityId);
        }
    }
}
