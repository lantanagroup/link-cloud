using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

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

    // Matches Scripts/seed_data/seed-facility-105-monthly.ps1's fixed values.
    private const string AchMonthlyMeasureName = "NHSN Acute Care Hospital Monthly Initial Population";
    private const string AchMonthlyDqm = "NHSNAcuteCareHospitalMonthlyInitialPopulation";
    private const string AchMonthlyComponent = "MSC";

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

    public async Task<bool> EnsureFacilityEnrolledInMeasureAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var mappingId = await EnsureAchMonthlyMeasureMappingAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var plansResponse = await _dmrpClient.GetFacilityReportingPlansForFacilityAsync(
            facilityId, month: now.Month, year: now.Year, cancellationToken: cancellationToken);
        var plans = LinkResponseHandler.Require(plansResponse, DmrpServiceName, nameof(EnsureFacilityEnrolledInMeasureAsync));

        if (plans.Any(plan => string.Equals(plan.MeasureMappingId, mappingId, StringComparison.Ordinal)))
        {
            return false;
        }

        var created = await _dmrpClient.CreateFacilityReportingPlanAsync(new FacilityReportingPlanRequest
        {
            FacilityId = facilityId,
            MeasureMappingId = mappingId,
            Component = AchMonthlyComponent,
            ReportingMonth = now.Month,
            ReportingYear = now.Year,
            IsReporting = true
        }, cancellationToken);

        // DMRP enforces one plan per facility/mapping/period -- a 409 here means a concurrent
        // enrollment won the race, not a real failure.
        if (created.StatusCode == StatusCodes.Status409Conflict)
        {
            return false;
        }
        LinkResponseHandler.EnsureSuccess(created, DmrpServiceName, nameof(EnsureFacilityEnrolledInMeasureAsync));

        return true;
    }

    private async Task<string> EnsureAchMonthlyMeasureMappingAsync(CancellationToken cancellationToken)
    {
        var existingId = await FindAchMonthlyMeasureMappingIdAsync(cancellationToken);
        if (existingId is not null)
        {
            return existingId;
        }

        var created = await _dmrpClient.CreateMeasureMappingAsync(new MeasureMappingModel
        {
            Measure = AchMonthlyMeasureName,
            DQM = AchMonthlyDqm,
            Frequency = Frequency.Monthly
        }, cancellationToken);

        // A 400 can mean a concurrent request already created the mapping -- re-search before
        // treating it as a real failure.
        if (created.StatusCode == StatusCodes.Status400BadRequest)
        {
            var concurrentId = await FindAchMonthlyMeasureMappingIdAsync(cancellationToken);
            if (concurrentId is not null)
            {
                return concurrentId;
            }
        }

        var mapping = LinkResponseHandler.Require(created, DmrpServiceName, nameof(EnsureAchMonthlyMeasureMappingAsync));
        return mapping.Id ?? throw new InvalidOperationException(
            $"DMRP created the '{AchMonthlyMeasureName}' measure mapping but returned no id.");
    }

    private async Task<string?> FindAchMonthlyMeasureMappingIdAsync(CancellationToken cancellationToken)
    {
        // DMRP allows only one mapping per measure, so measure is the only filter that can match
        // uniquely -- filtering on dQM/frequency too would miss an existing row under a different
        // frequency.
        var search = await _dmrpClient.SearchMeasureMappingsAsync(
            measure: AchMonthlyMeasureName, pageSize: 1, pageNumber: 1, cancellationToken: cancellationToken);
        var page = LinkResponseHandler.Optional(search, DmrpServiceName, nameof(FindAchMonthlyMeasureMappingIdAsync));
        var existing = page?.Records?.FirstOrDefault();

        if (existing is not null && !string.Equals(existing.DQM, AchMonthlyDqm, StringComparison.Ordinal))
        {
            // Reassigning an existing mapping's dQM is an admin decision, not this auto-enroll's --
            // reuse it as-is.
            _logger.LogWarning(
                "DMRP's mapping for '{Measure}' points at dQM '{ActualDqm}', not the expected '{ExpectedDqm}'. Reusing it as-is.",
                AchMonthlyMeasureName, existing.DQM, AchMonthlyDqm);
        }

        return existing?.Id;
    }
}
