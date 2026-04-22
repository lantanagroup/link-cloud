using System.Text.Json;
using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB-backed implementation of <see cref="IScenarioStore"/>.
/// </summary>
public sealed class MongoScenarioStore : IScenarioStore
{
    private readonly IMongoCollection<TestScenarioDocument> _scenarios;

    public MongoScenarioStore(IMongoDatabase database)
    {
        _scenarios = database.GetCollection<TestScenarioDocument>("automation_scenarios");
    }

    public async Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _scenarios.Find(FilterDefinition<TestScenarioDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);

        return docs.Select(ToModel).ToList();
    }

    public async Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _scenarios.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
    {
        var doc = ToDocument(scenario);
        await _scenarios.ReplaceOneAsync(
            d => d.Id == doc.Id,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _scenarios.DeleteOneAsync(d => d.Id == id, ct);
    }

    private static TestScenarioDocument ToDocument(TestScenarioDefinition model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        IsSystemScenario = model.IsSystemScenario,
        ReportMethod = model.ReportMethod.ToString(),
        SelectedMeasures = model.SelectedMeasures.Select(m => m.ToString()).ToList(),
        Seed = model.Seed,
        PatientCount = model.PatientCount,
        ResourcesPerPatientMin = model.ResourcesPerPatientMin,
        ResourcesPerPatientMax = model.ResourcesPerPatientMax,
        PatientPrefix = model.PatientPrefix,
        UseMeasureEligibilityProfiles = model.UseMeasureEligibilityProfiles,
        PatientProfilesJson = JsonSerializer.Serialize(model.PatientProfiles),
        PatientCohortsJson = JsonSerializer.Serialize(model.PatientCohorts),
        SelectedClinicalScenarioIds = model.SelectedClinicalScenarioIds,
        DischargeCount = model.DischargeCount,
        DischargeQualifyingCount = model.DischargeQualifyingCount,
        DischargeNonQualifyingCount = model.DischargeNonQualifyingCount,
        QueryPlanTemplateId = model.QueryPlanTemplateId,
        CleanupServiceData = model.CleanupServiceData,
        CleanupFhirData = model.CleanupFhirData,
        UpdatedAt = model.UpdatedAt
    };

    private static TestScenarioDefinition ToModel(TestScenarioDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        IsSystemScenario = doc.IsSystemScenario,
        ReportMethod = Enum.TryParse<ReportMethod>(doc.ReportMethod, true, out var rm) ? rm : ReportMethod.Adhoc,
        SelectedMeasures = doc.SelectedMeasures
            .Select(s => Enum.TryParse<ProfiledMeasureType>(s, true, out var m) ? m : (ProfiledMeasureType?)null)
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .ToList(),
        Seed = doc.Seed,
        PatientCount = doc.PatientCount,
        ResourcesPerPatientMin = doc.ResourcesPerPatientMin,
        ResourcesPerPatientMax = doc.ResourcesPerPatientMax,
        PatientPrefix = doc.PatientPrefix,
        UseMeasureEligibilityProfiles = doc.UseMeasureEligibilityProfiles,
        PatientProfiles = DeserializeProfiles(doc.PatientProfilesJson),
        PatientCohorts = DeserializeCohorts(doc.PatientCohortsJson),
        SelectedClinicalScenarioIds = doc.SelectedClinicalScenarioIds ?? [],
        DischargeCount = doc.DischargeCount,
        DischargeQualifyingCount = doc.DischargeQualifyingCount,
        DischargeNonQualifyingCount = doc.DischargeNonQualifyingCount,
        QueryPlanTemplateId = doc.QueryPlanTemplateId,
        CleanupServiceData = doc.CleanupServiceData,
        CleanupFhirData = doc.CleanupFhirData,
        UpdatedAt = doc.UpdatedAt
    };

    private static List<PatientProfile> DeserializeProfiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PatientProfile>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<PatientCohortDefinition> DeserializeCohorts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PatientCohortDefinition>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
