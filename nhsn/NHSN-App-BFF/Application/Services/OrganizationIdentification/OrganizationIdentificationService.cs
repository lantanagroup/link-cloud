using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.OrganizationIdentification;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.OrganizationIdentification;

public sealed class OrganizationIdentificationService : IOrganizationIdentificationService
{
    // Cerner's "Site" location type coding.
    private static readonly LocationTypeCodingResponse SiteTypeCoding = new()
    {
        System = "https://fhir.cerner.com/ecosystem/codeSet/72",
        Code = "783",
        Display = "Facility(s)"
    };

    // Matches the POC's fixture and MockApiClient. Fictional names, not real facility data.
    private static readonly string[] SimulatedSiteNames =
    [
        "Resurrection Medical Center",
        "UI Health",
        "Endeavor Health",
        "Gottlieb Memorial",
        "Loretto Hospital",
        "Community First Medical",
        "Humboldt Health"
    ];

    private readonly INhsnUserContext _userContext;

    public OrganizationIdentificationService(INhsnUserContext userContext)
    {
        _userContext = userContext;
    }

    public IReadOnlyList<LocationCandidateResponse> GetLocationCandidates(string method)
    {
        // Only the Cerner "Site" search has candidates today.
        if (!string.Equals(method, "location-type", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // The facility's own Location leads the list, matching a real Cerner search.
        var names = new List<string> { _userContext.FacilityName ?? "Unnamed Facility" };
        names.AddRange(SimulatedSiteNames);

        return names
            .Select((name, index) => new LocationCandidateResponse
            {
                Id = $"loc-site-{index + 1:000}",
                Display = name,
                TypeText = "Facility(s)",
                TypeCodings = [SiteTypeCoding]
            })
            .ToArray();
    }
}
