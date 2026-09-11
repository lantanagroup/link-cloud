using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IReportingPlanGateway over DMRP's reporting-plan periods and MeasureEval's measure-definition
// store. IDmrpServiceClient/IMeasureEvalServiceClient already run on LinkApiClientBase, which
// attaches a system token to every request -- no manual token handling needed here, unlike
// Tenant's FacilityManager.MeasureDefinitionExists, which predates that base class.
internal sealed class ReportingPlanGateway : IReportingPlanGateway
{
    private const string DmrpServiceName = "DMRP";
    private const string MeasureEvalServiceName = "MeasureEval";

    // One page is enough for any real facility's plan; a reporting plan is bounded by calendar
    // periods, not an open-ended collection.
    private const int MaxPeriodsPageSize = 100;

    private static readonly JsonSerializerOptions MeasureDefinitionOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDmrpServiceClient _dmrpClient;
    private readonly IMeasureEvalServiceClient _measureEvalClient;
    private readonly ILogger<ReportingPlanGateway> _logger;

    public ReportingPlanGateway(
        IDmrpServiceClient dmrpClient,
        IMeasureEvalServiceClient measureEvalClient,
        ILogger<ReportingPlanGateway> logger)
    {
        _dmrpClient = dmrpClient;
        _measureEvalClient = measureEvalClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableMeasure>> GetAvailableMeasuresAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var periodsResponse = await _dmrpClient.GetFacilityReportingPlanPeriodsAsync(
            facilityId, pageSize: MaxPeriodsPageSize, cancellationToken: cancellationToken);
        var periods = LinkResponseHandler.Require(periodsResponse, DmrpServiceName, nameof(GetAvailableMeasuresAsync));

        var supportedDqms = await GetSupportedDigitalQualityMeasuresAsync(cancellationToken);

        return periods.Records
            .SelectMany(period => period.Measures)
            .Where(measure => !string.IsNullOrWhiteSpace(measure.Measure) && !string.IsNullOrWhiteSpace(measure.DQM))
            .Where(measure => supportedDqms.Contains(measure.DQM!))
            .Select(measure => new AvailableMeasure {Name = measure.Measure!, DigitalQualityMeasure = measure.DQM!})
            .Distinct()
            .ToList();
    }

    // MeasureEval's list endpoint answers a raw JSON string (IMeasureEvalServiceClient.
    // GetAllMeasureDefinitionsAsync), not a typed body -- confirmed shape via a live GET:
    // [{"id": "...", "version": 1, "createdDate": ..., "modifiedDate": ...}, ...].
    private async Task<HashSet<string>> GetSupportedDigitalQualityMeasuresAsync(CancellationToken cancellationToken)
    {
        var response = await _measureEvalClient.GetAllMeasureDefinitionsAsync(cancellationToken);
        var body = LinkResponseHandler.Require(response, MeasureEvalServiceName, nameof(GetSupportedDigitalQualityMeasuresAsync));

        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        try
        {
            var definitions = JsonSerializer.Deserialize<List<MeasureDefinitionSummary>>(body, MeasureDefinitionOptions) ?? [];
            return definitions
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
                .Select(definition => definition.Id!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MeasureEval's measure-definition list could not be parsed; treating it as empty.");
            return [];
        }
    }

    private sealed record MeasureDefinitionSummary
    {
        public string? Id { get; init; }
    }
}
