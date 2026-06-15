using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Automation;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Centralised post-run cleanup logic.
/// Both the Automation UI and the backend E2E tests call through here
/// so cleanup behaviour stays consistent across consumers.
/// </summary>
public static class RunCleanupHelper
{
    /// <summary>
    /// Runs all post-run cleanup steps based on the flags in <paramref name="config"/>.
    /// </summary>
    public static async Task CleanupAfterRunAsync(
        TestScenarioConfig config,
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IReportServiceClient reportClient,
        FhirDataLoader fhirDataLoader,
        IAutomationOutput output,
        string facilityId,
        string? reportId)
    {
        if (config.CleanupServiceData)
        {
            await FacilitySetupHelper.CleanupFacilityAsync(
                facilityClient, normalizationClient, dataAcqClient, queryDispatchClient,
                output, facilityId);

            await FacilitySetupHelper.SoftDeleteRunDataAsync(
                reportClient, dataAcqClient, queryDispatchClient,
                output, facilityId, reportId ?? string.Empty);
        }

        if (config.CleanupFhirData)
        {
            fhirDataLoader.DeleteResourcesWithExpunge(output);
        }
    }

    /// <summary>
    /// Cleanup workflow for user-initiated cancellation.
    /// Order is: cancel DA work, soft-delete DA/report artifacts, then remove facility-level config,
    /// then expunge FHIR data.
    /// </summary>
    public static async Task CleanupCancelledRunAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IReportServiceClient reportClient,
        FhirDataLoader fhirDataLoader,
        IAutomationOutput output,
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            try
            {
                var cancelResult = await dataAcqClient.CancelAcquisitionLogsByFilterAsync(
                    new { FacilityId = facilityId },
                    minAgeHours: 0,
                    cancellationToken);

                output.WriteLine($"Requested DA cancellation for facility '{facilityId}' (cancelled={cancelResult?.Body?.Cancelled ?? 0}).");
            }
            catch (Exception ex)
            {
                output.WriteLine($"Warning: DA cancel-by-filter failed for facility '{facilityId}': {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(reportId))
                    await reportClient.SoftDeleteScheduleAsync(reportId);

                await dataAcqClient.SoftDeleteLogsByFacilityAsync(facilityId);
                await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);
                output.WriteLine($"Soft-deleted DA logs and report artifacts for facility '{facilityId}'.");
            }
            catch (Exception ex)
            {
                output.WriteLine($"Warning: soft-delete run data failed (facility={facilityId}, report={reportId}): {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            try
            {
                await FacilitySetupHelper.CleanupFacilityAsync(
                    facilityClient,
                    normalizationClient,
                    dataAcqClient,
                    queryDispatchClient,
                    output,
                    facilityId);
            }
            catch (Exception ex)
            {
                output.WriteLine($"Warning: facility cleanup failed for '{facilityId}': {ex.Message}");
            }
        }

        try
        {
            fhirDataLoader.DeleteResourcesWithExpunge(output);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: FHIR expunge failed during cancel cleanup: {ex.Message}");
        }
    }
}
