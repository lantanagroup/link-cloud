using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Run-engine entry for facility setup. The implementation is FacilityConfigurationService.
/// </summary>
public static class FacilitySetupHelper
{
    public static Task<bool> EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        CancellationToken cancellationToken = default,
        string? vendorName = null,
        bool vendorExplicit = false) =>
        FacilityConfigurationService.EnsureFacilityAsync(facilityClient, dmrpClient, output, facilityId, measureId, cancellationToken, vendorName, vendorExplicit);

    public static Task<bool> EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        CancellationToken cancellationToken = default,
        string? vendorName = null,
        bool vendorExplicit = false,
        Func<Task>? onCreated = null) =>
        FacilityConfigurationService.EnsureFacilityAsync(facilityClient, dmrpClient, output, facilityId, measureIds, cancellationToken, vendorName, vendorExplicit, onCreated);

    public static IReadOnlyList<(int Month, int Year)> GetDmrpReportingPeriods() =>
        FacilityConfigurationService.GetDmrpReportingPeriods();

    public static Task EnsureNormalizationConfigAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId) =>
        FacilityConfigurationService.EnsureNormalizationConfigAsync(normalizationClient, output, facilityId);

    public static Task EnsureQueryPlansAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        string ehrDescription,
        QueryPlanInput? externalQueryPlan = null) =>
        FacilityConfigurationService.EnsureQueryPlansAsync(dataAcqClient, output, facilityId, measureIds, ehrDescription, externalQueryPlan);

    public static Task EnsureQueryConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId,
        int? concurrencyOverride = null) =>
        FacilityConfigurationService.EnsureQueryConfigAsync(dataAcqClient, config, output, facilityId, concurrencyOverride);

    public static Task EnsureQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId) =>
        FacilityConfigurationService.EnsureQueryDispatchConfigAsync(queryDispatchClient, output, facilityId);

    public static Task EnsureCensusConfigAsync(
        ICensusServiceClient censusClient,
        IAutomationOutput output,
        string facilityId,
        string scheduledTrigger = "0 0/5 * * * ?",
        bool enabled = true) =>
        FacilityConfigurationService.EnsureCensusConfigAsync(censusClient, output, facilityId, scheduledTrigger, enabled);

    public static Task EnsureFhirListConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId) =>
        FacilityConfigurationService.EnsureFhirListConfigAsync(dataAcqClient, config, output, facilityId);

    public static Task CleanupQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId) =>
        FacilityConfigurationService.CleanupQueryDispatchConfigAsync(queryDispatchClient, output, facilityId);

    public static Task CleanupFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId) =>
        FacilityConfigurationService.CleanupFacilityAsync(facilityClient, normalizationClient, dataAcqClient, queryDispatchClient, output, facilityId);

    public static Task SoftDeleteRunDataAsync(
        IReportServiceClient reportClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId,
        string reportId) =>
        FacilityConfigurationService.SoftDeleteRunDataAsync(reportClient, dataAcqClient, queryDispatchClient, output, facilityId, reportId);

    public static Task<bool> EnsureEmptyDmrpFacilityAsync(
    IFacilityServiceClient facilityClient,
    IAutomationOutput output,
    string facilityId,
    CancellationToken cancellationToken = default,
    string? vendorName = null,
    bool vendorExplicit = false,
    Func<Task>? onCreated = null) =>
        FacilityConfigurationService.EnsureEmptyDmrpFacilityAsync(facilityClient, output, facilityId, cancellationToken, vendorName, vendorExplicit, onCreated);

    public static Task RefreshDmrpDerivedScheduleAsync(
    IFacilityServiceClient facilityClient,
    IAutomationOutput output,
    string facilityId,
    CancellationToken cancellationToken = default,
    string? vendorName = null,
    bool vendorExplicit = false) =>
        FacilityConfigurationService.RefreshDmrpDerivedScheduleAsync(facilityClient, output, facilityId, cancellationToken, vendorName, vendorExplicit);

    public static Task<string> EnsureDmrpMeasureMappingAsync(
    IDmrpServiceClient dmrpClient,
    IAutomationOutput output,
    string nhsnMeasure,
    string dqm,
    Frequency frequency,
    CancellationToken cancellationToken = default) =>
        FacilityConfigurationService.EnsureDmrpMeasureMappingAsync(dmrpClient, output, nhsnMeasure, dqm, frequency, cancellationToken);

}
