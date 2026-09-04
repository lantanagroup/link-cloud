using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Data Acquisition's Organization Identification rule. Read-modify-write: create, then replace.
public interface IOrganizationLocationConfigurationGateway
{
    // Null when the facility has no config yet - distinct from an empty method choice.
    Task<LocationOrgSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAsync(OrganizationLocationConfigurationSave request, CancellationToken cancellationToken = default);
}

public sealed record OrganizationLocationConfigurationSave
{
    public required string FacilityId { get; init; }
    public required LocationOrgSection LocationOrg { get; init; }
}
