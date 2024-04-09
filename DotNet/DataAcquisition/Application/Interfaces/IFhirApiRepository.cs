using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IFhirApiRepository
{
    Task<Bundle> GetPagedBundledResultsAsync(
        string baseUrl,
        Bundle bundle,
        PagedParameterQueryFactoryResult pagedQuery,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig);

    Task<Bundle> GetSingularBundledResultsAsync(
        string baseUrl,
        Bundle bundle,
        SingularParameterQueryFactoryResult query,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig);

    Task<Patient> GetPatient(string baseUrl, string patientId, AuthenticationConfiguration authConfig, CancellationToken cancellationToken = default);

    Task<List> GetPatientList(string baseUrl, string listId, AuthenticationConfiguration authConfig, CancellationToken cancellationToken = default);

    Task<List<DomainResource>> GetReferenceResource(
        string baseUrl,
        string resourceType,
        List<ResourceReference> referenceIds,
        ReferenceQueryConfig config,
        AuthenticationConfiguration authConfig);
}
