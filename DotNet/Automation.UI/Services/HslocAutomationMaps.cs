using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace Automation.UI.Services;

public static class HslocAutomationMaps
{
    public static List<NormalizationCodeSystemMap> BuildCodeSystemMaps(IReadOnlyList<string>? generatedPatientIds = null)
    {
        var maps = new List<NormalizationCodeSystemMap>
        {
            new()
            {
                SourceSystem = HslocMappingDefaults.RoleCodeSystem,
                TargetSystem = MappingTargetSystems.HslocUrl,
                CodeMaps = HslocMappingDefaults.RoleCodeToHsloc.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new NormalizationCodeMapEntry { Code = kvp.Value.Code, Display = kvp.Value.Display },
                    StringComparer.OrdinalIgnoreCase)
            }
        };

        var runTag = FhirGenerationPipeline.TryInferRunTag(generatedPatientIds ?? []);
        if (!string.IsNullOrWhiteSpace(runTag))
        {
            var ids = new FhirBundleGenerator.SharedIds(runTag);
            maps.Add(new NormalizationCodeSystemMap
            {
                SourceSystem = HslocMappingDefaults.IdentifierSystem,
                TargetSystem = MappingTargetSystems.HslocUrl,
                CodeMaps = new Dictionary<string, NormalizationCodeMapEntry>(StringComparer.Ordinal)
                {
                    [ids.IcuLocation] = Entry("ICU"),
                    [ids.EdLocation] = Entry("ER"),
                    [ids.StepDownLocation] = Entry("HU"),
                    [ids.HospitalLocation] = Entry("HOSP"),
                    [ids.OutpatientLocation] = Entry("OF")
                }
            });
        }

        return maps;
    }

    public static List<NormalizationCodeSystemMap> Merge(
        IReadOnlyList<NormalizationCodeSystemMap>? existing,
        IReadOnlyList<string>? generatedPatientIds)
    {
        var merged = new List<NormalizationCodeSystemMap>();
        foreach (var map in existing ?? [])
        {
            if (string.IsNullOrWhiteSpace(map.SourceSystem) || map.CodeMaps.Count == 0)
                continue;
            merged.Add(Clone(map));
        }

        foreach (var generated in BuildCodeSystemMaps(generatedPatientIds))
        {
            var match = merged.FirstOrDefault(m =>
                string.Equals(m.SourceSystem, generated.SourceSystem, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.TargetSystem, generated.TargetSystem, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                merged.Add(generated);
                continue;
            }

            foreach (var (code, entry) in generated.CodeMaps)
                match.CodeMaps[code] = entry;
        }

        return merged;
    }

    private static NormalizationCodeMapEntry Entry(string roleCode)
    {
        var mapped = HslocMappingDefaults.RoleCodeToHsloc[roleCode];
        return new NormalizationCodeMapEntry { Code = mapped.Code, Display = mapped.Display };
    }

    private static NormalizationCodeSystemMap Clone(NormalizationCodeSystemMap map) => new()
    {
        SourceSystem = map.SourceSystem,
        TargetSystem = map.TargetSystem,
        CodeMaps = new Dictionary<string, NormalizationCodeMapEntry>(map.CodeMaps, StringComparer.OrdinalIgnoreCase)
    };
}
