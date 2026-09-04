namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Builds catalog rows from the hardcoded story-pack tables so existing pickers
/// keep working before measure value-set import runs.
/// </summary>
public static class GenerationCatalogSeed
{
    public static List<GenerationCatalogItem> FromHardcoded()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<GenerationCatalogItem>();

        foreach (var c in FhirGenerationCodes.Conditions)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Condition,
                System = GenerationCatalogItem.Snomed,
                Code = c.Code,
                Display = c.Display,
                Category = c.Category,
                IcdCode = c.IcdCode,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var s in FhirGenerationCodes.ClinicalScenarios)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Condition,
                System = GenerationCatalogItem.Snomed,
                Code = s.PrimaryDxSnomed,
                Display = s.PrimaryDxDisplay,
                Category = "encounter-diagnosis",
                IcdCode = s.PrimaryDxIcd,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var o in FhirGenerationCodes.Observations)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Observation,
                System = GenerationCatalogItem.Loinc,
                Code = o.Code,
                Display = o.Display,
                Category = o.Category,
                Unit = string.IsNullOrWhiteSpace(o.Unit) ? null : o.Unit,
                NormLow = o.NormLow,
                NormHigh = o.NormHigh,
                Incomplete = string.IsNullOrWhiteSpace(o.Unit),
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var p in FhirGenerationCodes.Procedures)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Procedure,
                System = GenerationCatalogItem.Snomed,
                Code = p.Code,
                Display = p.Display,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var m in FhirGenerationCodes.Medications)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Medication,
                System = GenerationCatalogItem.RxNorm,
                Code = m.RxCode,
                Display = m.Display,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var s in FhirGenerationCodes.ServiceRequests)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.ServiceRequest,
                System = string.IsNullOrWhiteSpace(s.System) ? GenerationCatalogItem.Loinc : s.System,
                Code = s.Code,
                Display = s.Display,
                IsLab = s.IsLab,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        foreach (var s in FhirGenerationCodes.Specimens)
        {
            items.Add(new GenerationCatalogItem
            {
                Kind = GenerationCatalogKind.Specimen,
                System = string.IsNullOrWhiteSpace(s.TypeSystem) ? GenerationCatalogItem.SpecimenType : s.TypeSystem,
                Code = s.TypeCode,
                Display = s.TypeDisplay,
                IsSeed = true,
                UpdatedAt = now
            });
        }

        return Dedupe(items);
    }

    public static List<GenerationCatalogItem> Dedupe(IEnumerable<GenerationCatalogItem> items)
    {
        var map = new Dictionary<string, GenerationCatalogItem>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
                continue;
            item.System = GenerationCatalogItem.GuessSystem(item.Kind, item.System, item.Code);
            map[item.DedupKey] = item;
        }

        return map.Values.ToList();
    }
}
