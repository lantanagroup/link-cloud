using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public record SearchFhirCommandRequest(
    FhirQueryConfiguration queryConfig, 
    ResourceType resourceType, 
    SearchParams searchParams, 
    string? facilityId, 
    string? patientId, 
    string? correlationId, 
    QueryPhase? queryPhase);

public interface ISearchFhirCommand
{
    IAsyncEnumerable<Bundle> ExecuteAsync(
        SearchFhirCommandRequest request,
        CancellationToken cancellationToken = default);
    Task<Bundle> ExecuteRequestAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken = default);
}

public class SearchFhirCommand : ISearchFhirCommand
{
    private readonly ILogger<SearchFhirCommand> _logger;
    private readonly HttpClient _httpClient;
    private readonly IDataAcquisitionServiceMetrics _metrics;

    public SearchFhirCommand(ILogger<SearchFhirCommand> logger, HttpClient httpClient, IDataAcquisitionServiceMetrics metrics)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async IAsyncEnumerable<Bundle> ExecuteAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, _httpClient, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        });
        var resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);

        yield return resultBundle;

        Bundle? newResultBundle = resultBundle;

        if (newResultBundle != null)
        {
            while (resultBundle.Link.Exists(x => x.Relation == "next"))
            {
                resultBundle = await fhirClient.ContinueAsync(resultBundle, ct: cancellationToken);
                if (resultBundle != null && resultBundle.Entry.Any())
                {
                    yield return resultBundle;
                    IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
                }
            }
        }
    }

    public async Task<Bundle> ExecuteRequestAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, _httpClient, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        });

        var resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
        IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
        return resultBundle;
    }

    private void IncrementResourceAcquiredMetric(string? correlationId, string? patientIdReference, string? facilityId, string? queryType, string resourceType, string resourceId)
    {
        _metrics.IncrementResourceAcquiredCounter([
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientIdReference), //TODO: Can we keep this?
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType),
            new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resourceId)
        ]);
    }
}
