using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public static partial class FacilityConfigurationService
{
    public static async Task<FacilitySectionResult> CreateFacilityAsync(
        IFacilityServiceClient facilities,
        FacilityModel model,
        CancellationToken cancellationToken)
    {
        var response = await facilities.CreateAsync(model, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> UpdateFacilityAsync(
        IFacilityServiceClient facilities,
        string facilityId,
        FacilityModel model,
        CancellationToken cancellationToken)
    {
        var response = await facilities.UpdateAsync(facilityId, model, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SoftDeleteFacilityAsync(
        IFacilityServiceClient facilities,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await facilities.SoftDeleteAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveCensusAsync(
        ICensusServiceClient census,
        CensusConfigApiModel request,
        bool exists,
        CancellationToken cancellationToken)
    {
        var response = exists
            ? await census.UpdateCensusConfigAsync(request.FacilityId!, request, cancellationToken)
            : await census.CreateCensusConfigAsync(request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteCensusAsync(
        ICensusServiceClient census,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await census.DeleteCensusConfigAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveQueryDispatchAsync(
        IQueryDispatchServiceClient queryDispatch,
        QueryDispatchConfigurationApiModel request,
        bool exists,
        CancellationToken cancellationToken)
    {
        var response = exists
            ? await queryDispatch.UpsertQueryDispatchConfigurationAsync(request.FacilityId!, request, cancellationToken)
            : await queryDispatch.CreateQueryDispatchConfigurationAsync(request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteQueryDispatchAsync(
        IQueryDispatchServiceClient queryDispatch,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await queryDispatch.DeleteQueryDispatchConfigurationAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveFhirQueryAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        object body,
        bool exists,
        CancellationToken cancellationToken)
    {
        var response = exists
            ? await dataAcquisition.UpdateFhirQueryConfigurationAsync(body, cancellationToken)
            : await dataAcquisition.CreateFhirQueryConfigurationAsync(body, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteFhirQueryAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteFhirQueryConfigurationAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveFhirListAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        object body,
        bool exists,
        CancellationToken cancellationToken)
    {
        var response = exists
            ? await dataAcquisition.UpdateFhirListConfigurationAsync(body, cancellationToken)
            : await dataAcquisition.CreateFhirListConfigurationAsync(body, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteFhirListAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteFhirListConfigurationAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> CreateQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        CreateQueryPlanRequestApiModel request,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.CreateQueryPlanAsync(facilityId, request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> UpdateQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        object request,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.UpdateQueryPlanAsync(facilityId, request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        string planType,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteQueryPlanAsync(facilityId, planType, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveReportingOrganizationAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        CreateOrganizationLocationConfigurationApiModel body,
        bool exists,
        int? configId,
        CancellationToken cancellationToken)
    {
        var response = exists && configId is int id
            ? await dataAcquisition.UpdateOrganizationLocationConfigurationByIdAsync(id, body, cancellationToken)
            : await dataAcquisition.CreateOrganizationLocationConfigurationAsync(facilityId, body, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteReportingOrganizationAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        int configId,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteOrganizationLocationConfigurationByIdAsync(configId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveSftpAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        object body,
        bool exists,
        string? configurationId,
        CancellationToken cancellationToken)
    {
        var response = exists
            ? await dataAcquisition.UpdateSftpConfigurationAsync(facilityId, configurationId ?? string.Empty, body, cancellationToken)
            : await dataAcquisition.CreateSftpConfigurationAsync(facilityId, body, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> UpdateSftpCredentialsAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        object credentials,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.UpdateSftpCredentialsAsync(facilityId, credentials, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteSftpAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        string configurationId,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteSftpConfigurationAsync(facilityId, configurationId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteSftpCredentialsAsync(
        IDataAcquisitionServiceClient dataAcquisition,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await dataAcquisition.DeleteSftpCredentialsAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> CreateNormalizationOperationAsync(
        INormalizationServiceClient normalization,
        CreateNormalizationOperationRequestApiModel request,
        CancellationToken cancellationToken)
    {
        var response = await normalization.CreateOperationAsync(request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> UpdateNormalizationOperationAsync(
        INormalizationServiceClient normalization,
        UpdateNormalizationOperationRequestApiModel request,
        CancellationToken cancellationToken)
    {
        var response = await normalization.UpdateOperationAsync(request, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteNormalizationOperationsAsync(
        INormalizationServiceClient normalization,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var response = await normalization.DeleteFacilityOperationsAsync(facilityId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteNormalizationOperationAsync(
        INormalizationServiceClient normalization,
        string facilityId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var response = await normalization.DeleteFacilityOperationAsync(facilityId, operationId, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> CreateNormalizationSequencesAsync(
        INormalizationServiceClient normalization,
        string facilityId,
        string resourceType,
        List<CreateNormalizationOperationSequenceApiModel> sequences,
        CancellationToken cancellationToken)
    {
        var response = await normalization.CreateOperationSequencesAsync(facilityId, resourceType, sequences, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteNormalizationSequencesAsync(
        INormalizationServiceClient normalization,
        string facilityId,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        var response = await normalization.DeleteOperationSequencesAsync(facilityId, resourceType, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> SaveNotificationConfigurationAsync(
        INotificationServiceClient notification,
        NotificationConfigurationApiModel model,
        bool creating,
        CancellationToken cancellationToken)
    {
        var response = creating
            ? await notification.CreateConfigurationAsync(model, cancellationToken)
            : await notification.UpdateConfigurationAsync(model, cancellationToken);
        return FacilitySectionResult.From(response);
    }

    public static async Task<FacilitySectionResult> DeleteNotificationConfigurationAsync(
        INotificationServiceClient notification,
        string id,
        CancellationToken cancellationToken)
    {
        var response = await notification.DeleteConfigurationAsync(id, cancellationToken);
        return FacilitySectionResult.From(response);
    }
}
