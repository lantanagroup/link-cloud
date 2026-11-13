using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.OrganizationIdentification;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Backs the Cerner "Site" location search. No live Data Acquisition query yet - results are simulated.
public interface IOrganizationIdentificationService
{
    IReadOnlyList<LocationCandidateResponse> GetLocationCandidates(string method);
}
