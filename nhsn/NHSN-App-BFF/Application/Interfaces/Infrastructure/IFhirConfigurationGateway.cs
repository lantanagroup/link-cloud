using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Data Acquisition's FHIR query configuration, in our vocabulary.
//
// Read-only for now: DataAcquisitionServiceClient exposes no update operation on any configuration
// resource, so the write half of read-modify-write can't be executed through it yet. The write
// method lands here unchanged in shape once the SDK adds it.
public interface IFhirConfigurationGateway
{
    // Reads the facility's FHIR configuration, or null when none exists yet.
    Task<FhirSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default);
}
