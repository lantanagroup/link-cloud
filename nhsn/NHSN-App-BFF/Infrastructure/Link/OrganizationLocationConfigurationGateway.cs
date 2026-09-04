using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// Read-modify-write, same shape as FhirConfigurationGateway.SaveAsync: existing config decides POST vs PUT.
internal sealed class OrganizationLocationConfigurationGateway : IOrganizationLocationConfigurationGateway
{
    private const string ServiceName = "DataAcquisition";

    private readonly IDataAcquisitionServiceClient _dataAcquisitionClient;
    private readonly IDataAcquisitionRawClient _rawClient;

    public OrganizationLocationConfigurationGateway(IDataAcquisitionServiceClient dataAcquisitionClient, IDataAcquisitionRawClient rawClient)
    {
        _dataAcquisitionClient = dataAcquisitionClient;
        _rawClient = rawClient;
    }

    public async Task<LocationOrgSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcquisitionClient.GetOrganizationLocationConfigurationsAsync(facilityId, cancellationToken);
        var configs = LinkResponseHandler.Optional(response, ServiceName, nameof(GetAsync));
        if (configs is null || configs.Count == 0)
        {
            return null;
        }

        // SaveAsync keeps every config for the facility in lockstep (facility-wide replace), so any
        // one of them - the active one if there is one - reflects the current rule.
        var current = configs.FirstOrDefault(config => config.IsActive) ?? configs[0];
        var orderedConditions = current.Conditions
            .OrderBy(condition => condition.Priority)
            .Select(condition => condition.FhirPath ?? string.Empty)
            .ToList();
        return LocationOrgFhirPathParser.Parse(orderedConditions);
    }

    public async Task SaveAsync(OrganizationLocationConfigurationSave request, CancellationToken cancellationToken = default)
    {
        var conditions = LocationOrgFhirPathBuilder.Build(request.LocationOrg);
        if (conditions.Count == 0)
        {
            // Nothing on this step is mandatory.
            return;
        }

        var description = $"NHSNLink Organization Identification ({request.LocationOrg.Method})";

        var existingResponse = await _dataAcquisitionClient.GetOrganizationLocationConfigurationsAsync(request.FacilityId, cancellationToken);
        var existing = LinkResponseHandler.Optional(existingResponse, ServiceName, nameof(SaveAsync));

        if (existing is null || existing.Count == 0)
        {
            var createResponse = await _dataAcquisitionClient.CreateOrganizationLocationConfigurationAsync(
                request.FacilityId,
                new CreateOrganizationLocationConfigurationApiModel
                {
                    Description = description,
                    IsActive = true,
                    Conditions = conditions
                        .Select(condition => new CreateOrganizationLocationConditionApiModel
                        {
                            FhirPath = condition.FhirPath,
                            Priority = condition.Priority
                        })
                        .ToList()
                },
                cancellationToken);

            LinkResponseHandler.EnsureSuccess(createResponse, ServiceName, nameof(SaveAsync));
            return;
        }

        // Facility-wide replace.
        await _rawClient.UpdateOrganizationLocationConfigurationAsync(
            request.FacilityId,
            new UpdateOrganizationLocationConfigurationPayload
            {
                Description = description,
                IsActive = true,
                Conditions = conditions
                    .Select(condition => new UpdateOrganizationLocationConditionPayload
                    {
                        FhirPath = condition.FhirPath,
                        Priority = condition.Priority
                    })
                    .ToList()
            },
            cancellationToken);
    }
}
