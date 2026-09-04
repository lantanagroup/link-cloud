using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Builds Data Acquisition's FHIRPath conditions from the structured draft. One per row, priority = row order.
internal static class LocationOrgFhirPathBuilder
{
    public static List<(string FhirPath, int Priority)> Build(LocationOrgSection section)
    {
        var conditions = section.Method switch
        {
            "managing-org" => section.ManagingOrganizationIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => $"Location.managingOrganization.reference = 'Organization/{id}'"),

            "location-type" => section.LocationTypes
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Code) && !string.IsNullOrWhiteSpace(entry.Alias))
                .Select(entry =>
                    $"Location.type.coding.where(code = '{entry.Code}').exists() and Location.alias.contains('{entry.Alias}')"),

            "location-identifier" => section.LocationIdentifiers
                .Where(entry => !string.IsNullOrWhiteSpace(entry.System) && !string.IsNullOrWhiteSpace(entry.Code))
                .Select(entry =>
                    $"Location.identifier.where(system = '{entry.System}' and value = '{entry.Code}').exists()"),

            "custom-fhir-path" when !string.IsNullOrWhiteSpace(section.CustomFhirPath) =>
                [section.CustomFhirPath!],

            _ => Enumerable.Empty<string>()
        };

        return conditions
            .Select((fhirPath, index) => (FhirPath: fhirPath, Priority: index + 1))
            .ToList();
    }
}
