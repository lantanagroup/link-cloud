namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

/// <summary>Wire shape for the facility-wide PUT - not wrapped by LinkSdk yet.</summary>
public sealed class UpdateOrganizationLocationConfigurationPayload
{
    public string? Description { get; init; }
    public bool? IsActive { get; init; }
    public List<UpdateOrganizationLocationConditionPayload> Conditions { get; init; } = [];
}

public sealed class UpdateOrganizationLocationConditionPayload
{
    public required string FhirPath { get; init; }
    public required int Priority { get; init; }
}
