using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public interface ISearchFhirCommand
{
    Task<Bundle> ExecuteAsync(
        string facilityId,
        ResourceType resourceType,
        SearchParams searchParams,
        CancellationToken cancellationToken = default);
}

public class SearchFhirCommand
{
}
