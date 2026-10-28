using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Automation-owned FHIR fixture helpers used after Thetis synthesizes a patient:
/// shared org/location/practitioner/medication infrastructure, per-patient
/// anchors, Hypoglycemic insulin overlay, and generation-requirement stamps.
/// Per-patient clinical resources come from Thetis Engine.
/// </summary>
internal static class ScenarioResourceGeneration
{
    // ------------------------------------------------------------------
    //  Bundle entry construction + serialization
    // ------------------------------------------------------------------

    /// <summary>
    /// Wraps a FHIR resource in a transaction-bundle entry with a PUT request
    /// (idempotent upsert by ID). The <c>FullUrl</c> base is a placeholder —
    /// real upload happens through the configured FHIR client, which rewrites
    /// it as needed.
    /// </summary>
    internal static Bundle.EntryComponent Entry(string resourceUrl, Resource resource)
    {
        return new Bundle.EntryComponent
        {
            Resource = resource,
            FullUrl = $"http://localhost:8080/fhir/{resourceUrl}",
            Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = resourceUrl }
        };
    }

    private static void AddTopLevelExtensionIfMissing(DomainResource resource, string url, DataType value)
    {
        if (resource.Extension.Any(e => string.Equals(e.Url, url, StringComparison.Ordinal)))
            return;

        resource.Extension.Add(new Extension(url, value));
    }

    private static void AddLocationCopyOperationOpportunities(Location location)
    {
        var hasUsableIdentifier = location.Identifier.Any(i =>
            !string.IsNullOrWhiteSpace(i.System) &&
            !string.IsNullOrWhiteSpace(i.Value));

        if (hasUsableIdentifier)
            return;

        var fallbackId = string.IsNullOrWhiteSpace(location.Id)
            ? BuildDeterministicLocationIdentifier(location)
            : location.Id;

        location.Identifier.Add(new Identifier
        {
            System = "http://example.org/fhir/sid/location",
            Value = fallbackId
        });
    }

    private static string BuildDeterministicLocationIdentifier(Location location)
    {
        var codingSignature = string.Join(",",
            location.Type
                .SelectMany(t => t.Coding)
                .Select(c => $"{c.System}|{c.Code}")
                .OrderBy(s => s, StringComparer.Ordinal));

        var stableInput = string.Join("|",
            location.Name ?? string.Empty,
            location.ManagingOrganization?.Reference ?? string.Empty,
            codingSignature,
            location.PhysicalType?.Text ?? string.Empty);

        if (string.IsNullOrWhiteSpace(stableInput))
            stableInput = "location";

        return $"loc-{stableInput.GetStableHash32():X8}";
    }

    /// <summary>
    /// Serializes a list of bundle entries as a transaction-type FHIR Bundle.
    /// Uses the without-validation serializer because the generator emits
    /// well-formed resources by construction; full validation here would just
    /// duplicate the FHIR server's own validation pass.
    /// </summary>
    internal static string Serialize(List<Bundle.EntryComponent> entries)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction, Entry = entries };
        return JsonSerializer.Serialize(bundle, FhirSerializerOptions.ForFhirWithoutValidation());
    }

    // ------------------------------------------------------------------
    //  Shared infrastructure (Organization / Locations / Devices /
    //  Practitioners / formulary)
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the run-scoped shared infrastructure block: Organization, five
    /// Locations (Hospital / ICU / ED / Step-Down / Outpatient), three Devices
    /// (Pulse oximeter / Ventilator / CPAP), the full Practitioner roster, and
    /// the formulary Medications. Every per-patient resource that follows
    /// references one or more of these by ID.
    ///
    /// Returns the entry list plus the IDs needed by per-patient generation
    /// (Practitioner picks for attending/admitting/GP rotations; Medications
    /// for MedicationRequest/MedicationAdministration RxNorm matching).
    /// </summary>
    internal static (List<Bundle.EntryComponent> Entries, List<string> PractitionerIds, List<string> MedicationIds)
        BuildSharedInfrastructure(FhirBundleGenerator.SharedIds ids, GenerationRequirementsPlan? generationRequirementsPlan = null)
    {
        var entries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{ids.Organization}",   OrganizationFactory.Generate(ids.Organization)),
            Entry($"Location/{ids.HospitalLocation}",   LocationFactory.Generate(ids.HospitalLocation,  "HOSP", "Main Hospital",        ids.Organization)),
            Entry($"Location/{ids.IcuLocation}",        LocationFactory.Generate(ids.IcuLocation,       "ICU",  "Intensive Care Unit",  ids.Organization, ids.HospitalLocation)),
            Entry($"Location/{ids.EdLocation}",         LocationFactory.Generate(ids.EdLocation,        "ER",   "Emergency Department", ids.Organization, ids.HospitalLocation)),
            Entry($"Location/{ids.StepDownLocation}",   LocationFactory.Generate(ids.StepDownLocation,  "HU",   "Step-Down Unit",       ids.Organization, ids.HospitalLocation)),
            Entry($"Location/{ids.OutpatientLocation}", LocationFactory.Create  (ids.OutpatientLocation,"OF",   "Outpatient Clinic",    ids.Organization, ids.HospitalLocation)),
            Entry($"Device/{ids.DevicePulseOx}",        DeviceFactory.Create    (ids.DevicePulseOx,    "706689003", "Pulse oximeter",                             null)),
            Entry($"Device/{ids.DeviceVentilator}",     DeviceFactory.Create    (ids.DeviceVentilator, "706172005", "Ventilator",                                 null)),
            Entry($"Device/{ids.DeviceCPAP}",           DeviceFactory.Create    (ids.DeviceCPAP,       "10776007",  "Continuous positive airway pressure device", null)),
        };

        var practitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = ids.PractitionerId(pi);
            practitionerIds.Add(practId);
            entries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        var medicationIds = GenerateSharedMedications(entries, ids);

        ApplyGenerationRequirements(entries, generationRequirementsPlan);

        return (entries, practitionerIds, medicationIds);
    }

    /// <summary>
    /// Generates shared hospital formulary Medication resources — one per
    /// <see cref="FhirGenerationCodes.Medications"/> entry — plus the separate
    /// Hypoglycemic-measure-specific insulin glargine concept.
    ///
    /// In a real EHR the formulary is facility-level; every patient's
    /// MedicationRequest / MedicationAdministration references the same
    /// Medication resource, which is what this models.
    /// </summary>
    internal static List<string> GenerateSharedMedications(
        List<Bundle.EntryComponent> sharedEntries, FhirBundleGenerator.SharedIds ids)
    {
        var medIds = new List<string>(FhirGenerationCodes.Medications.Length + 1);
        for (var i = 0; i < FhirGenerationCodes.Medications.Length; i++)
        {
            var v = FhirGenerationCodes.Medications[i];
            var medId = ids.MedicationId(i);
            medIds.Add(medId);
            sharedEntries.Add(Entry($"Medication/{medId}",
                MedicationFactory.Create(medId, v.RxCode, v.Display, v.DoseValue, v.DoseUnit, v.RouteCode, v.RouteDisplay)));
        }

        // Hypoglycemic-measure-specific insulin glargine (RxNorm 274783) —
        // a separate clinical drug concept from the formulary entry above.
        sharedEntries.Add(Entry($"Medication/{ids.HypoInsulinGlargineMedication}",
            MedicationFactory.Create(ids.HypoInsulinGlargineMedication, "274783", "insulin glargine",
                20, "[iU]", "34206005", "Subcutaneous route")));

        return medIds;
    }


    // ------------------------------------------------------------------
    //  Per-patient core-anchor composition — shared by the bulk
    //  (FhirBundleGenerator) and streaming (FhirGenerationPipeline) paths.
    // ------------------------------------------------------------------

    /// <summary>
    /// Per-patient identifiers and rotation-picked practitioner IDs that both
    /// generation paths need. Computed once per patient via
    /// <see cref="ComputePatientAnchors"/> so the two callers can never drift
    /// on ID shape (e.g. <c>{patientId}-Enc-001</c>) or on which seed offsets
    /// pick which practitioner role.
    /// </summary>
    internal sealed record PatientAnchorContext(
        string EncounterId,
        string CareTeamId,
        string CarePlanId,
        string PatientDeviceId,
        string PrimaryDxId,
        string AttendingPractId,
        string AdmittingPractId,
        string GpPractId);

    /// <summary>
    /// Builds the per-patient anchor IDs and selects the
    /// attending / admitting / GP practitioner picks from the shared roster
    /// using the seed-rotation scheme that both generation paths share.
    /// </summary>
    internal static PatientAnchorContext ComputePatientAnchors(
        string patientId, int patientSeed, List<string> sharedPractitionerIds)
    {
        var attendingPractId = sharedPractitionerIds[FhirBundleGenerator.Mod(patientSeed, sharedPractitionerIds.Count)];
        var admittingPractId = sharedPractitionerIds[FhirBundleGenerator.Mod(patientSeed + 1, sharedPractitionerIds.Count)];
        var gpPractId = sharedPractitionerIds[FhirBundleGenerator.Mod(patientSeed + 2, sharedPractitionerIds.Count)];
        return new PatientAnchorContext(
            EncounterId: $"{patientId}-Enc-001",
            CareTeamId: $"{patientId}-CareTeam-001",
            CarePlanId: $"{patientId}-CarePlan-001",
            PatientDeviceId: $"{patientId}-Device-001",
            PrimaryDxId: $"{patientId}-Condition-primary",
            AttendingPractId: attendingPractId,
            AdmittingPractId: admittingPractId,
            GpPractId: gpPractId);
    }

    /// <summary>
    /// Emits the Hypoglycemic-measure-qualifying MedicationRequest +
    /// MedicationAdministration pair (insulin glargine, RxNorm 274783) that
    /// references the run-scoped shared <c>HypoInsulinGlargineMedication</c>.
    /// Used when Thetis does not already emit a qualifying insulin pair
    /// (<c>PatientProfile.RequiresHypoglycemicMedication</c>).
    /// </summary>
    internal static void AddHypoglycemicQualifyingMedicationEntries(
        List<Bundle.EntryComponent> entries,
        string patientId,
        string encounterId,
        string practitionerId,
        int seed,
        DateTime encounterStart,
        DateTime encounterEnd,
        FhirBundleGenerator.SharedIds ids,
        DateTime? measurementPeriodStart = null,
        DateTime? measurementPeriodEnd = null)
    {
        const string insulinRxNorm = "274783";
        const string insulinDisplay = "insulin glargine";
        const string subcutaneousRouteCode = "34206005";
        const string subcutaneousRouteDisplay = "Subcutaneous route";
        const string diabetesIndicationCode = "44054006";
        const string diabetesIndicationDisplay = "Diabetes mellitus type 2";

        var medicationRequestId = $"{patientId}-MedReq-A01";
        var medicationAdministrationId = $"{patientId}-MedAdm-A01";
        var medicationTime = encounterStart.AddHours(1);

        // For long-stay encounters that start before the measurement period,
        // anchor the qualifying anti-diabetic medication pair inside the actual
        // encounter∩measurement overlap window so Hypoglycemic-IP CQL predicates
        // that apply period-aware date constraints can still match deterministically.
        if (measurementPeriodStart.HasValue && measurementPeriodEnd.HasValue)
        {
            var overlapStart = encounterStart > measurementPeriodStart.Value
                ? encounterStart
                : measurementPeriodStart.Value;
            var overlapEnd = encounterEnd < measurementPeriodEnd.Value
                ? encounterEnd
                : measurementPeriodEnd.Value;

            if (overlapEnd >= overlapStart)
            {
                var candidate = overlapStart.AddHours(1);
                medicationTime = candidate <= overlapEnd ? candidate : overlapStart;
            }
        }

        entries.Add(Entry($"MedicationRequest/{medicationRequestId}",
            MedicationRequestFactory.Create(
                medicationRequestId, patientId, encounterId, medicationTime, seed, practitionerId,
                insulinRxNorm, insulinDisplay, subcutaneousRouteCode, subcutaneousRouteDisplay,
                20, "[iU]", 1, false,
                diabetesIndicationCode, diabetesIndicationDisplay,
                null, ids.HypoInsulinGlargineMedication)));

        entries.Add(Entry($"MedicationAdministration/{medicationAdministrationId}",
            MedicationAdministrationFactory.Create(
                medicationAdministrationId, patientId, encounterId, medicationTime, seed, practitionerId,
                insulinRxNorm, insulinDisplay, subcutaneousRouteCode, subcutaneousRouteDisplay,
                20, "[iU]",
                diabetesIndicationCode, diabetesIndicationDisplay,
                false, ids.HypoInsulinGlargineMedication)));
    }

    /// <summary>
    /// Stamps consumer-owned test-fixture opportunities onto already-generated FHIR
    /// (extensions the suite will remove, identifiers org-location maps match, etc.).
    /// Thetis/classic synthesis stays clinical; this pass is how Automation aligns
    /// payload shape to the selected normalization suite and organization resource map
    /// without teaching the engine those products.
    /// </summary>
    /// <returns>Number of resource/requirement applications performed.</returns>
    internal static int ApplyGenerationRequirements(
        List<Bundle.EntryComponent> entries,
        GenerationRequirementsPlan? generationRequirementsPlan)
    {
        if (generationRequirementsPlan == null || generationRequirementsPlan.Requirements.Count == 0)
            return 0;

        var resourcesByType = entries
            .Where(e => e.Resource != null)
            .GroupBy(e => e.Resource!.TypeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Resource!).ToList(), StringComparer.OrdinalIgnoreCase);

        var applied = 0;
        foreach (var requirement in generationRequirementsPlan.Requirements)
        {
            foreach (var resourceType in requirement.ResourceTypes.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!resourcesByType.TryGetValue(resourceType, out var candidates) || candidates.Count == 0)
                    continue;

                // Stamp every candidate for ops that must fire per acquired resource
                // (RemoveExtensions, Location copy/map). A single first-of-type stamp is
                // lost when that resource is not acquired (e.g. an Observation dated
                // before the report window).
                if (AppliesToEveryCandidate(requirement.RequirementType))
                {
                    foreach (var candidate in candidates)
                    {
                        ApplyRequirement(requirement, candidate);
                        applied++;
                    }
                }
                else
                {
                    ApplyRequirement(requirement, candidates[0]);
                    applied++;
                }
            }
        }

        return applied;
    }

    private static bool AppliesToEveryCandidate(string requirementType) =>
        string.Equals(requirementType, "OrganizationLocationMapping", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requirementType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requirementType, "CopyLocation", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requirementType, "CopyProperty", StringComparison.OrdinalIgnoreCase);

    private static void ApplyRequirement(GenerationRequirement requirement, Resource resource)
    {
        switch (requirement.RequirementType)
        {
            case "RemoveExtensions":
                if (resource is DomainResource dr)
                    EnsureExtensionUrls(dr, requirement.ExtensionUrls);
                break;

            case "CopyLocation":
                if (resource is Location loc)
                    AddLocationCopyOperationOpportunities(loc);
                break;

            case "CopyProperty":
                EnsureSourcePathOpportunity(resource, requirement.SourceFhirPath);
                break;

            case "CodeMap":
                EnsureCodeMapOpportunity(resource, requirement);
                break;

            case "ConditionalTransform":
                EnsureConditionalOpportunity(resource, requirement);
                break;

            case "OrganizationLocationMapping":
                if (resource is Location mapLocation)
                    EnsureOrganizationLocationMappingOpportunity(mapLocation, requirement.SourceFhirPath);
                break;
        }
    }

    private static void EnsureOrganizationLocationMappingOpportunity(Location location, string? mappingFhirPath)
    {
        if (string.IsNullOrWhiteSpace(mappingFhirPath))
            return;

        var path = mappingFhirPath.Trim();
        if (path.StartsWith("Location.", StringComparison.OrdinalIgnoreCase))
            path = path["Location.".Length..];

        static string? ExtractQuotedValue(string source, string key)
        {
            var match = Regex.Match(source, $@"\b{Regex.Escape(key)}\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        var system = ExtractQuotedValue(path, "system");
        var value = ExtractQuotedValue(path, "value");
        var code = ExtractQuotedValue(path, "code");

        if (path.Contains("identifier", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(system))
        {
            location.Identifier ??= [];
            var hasIdentifier = location.Identifier.Any(i =>
                string.Equals(i.System, system, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(value) || string.Equals(i.Value, value, StringComparison.OrdinalIgnoreCase)));

            if (!hasIdentifier)
            {
                location.Identifier.Add(new Identifier
                {
                    System = system,
                    Value = string.IsNullOrWhiteSpace(value) ? BuildDeterministicLocationIdentifier(location) : value
                });
            }
        }

        if (path.Contains("type.coding", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(system))
        {
            location.Type ??= [];
            if (location.Type.Count == 0)
                location.Type.Add(new CodeableConcept());

            var coding = location.Type[0].Coding ??= [];
            var hasCoding = coding.Any(c =>
                string.Equals(c.System, system, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(code) || string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)));

            if (!hasCoding)
            {
                coding.Add(new Coding
                {
                    System = system,
                    Code = string.IsNullOrWhiteSpace(code) ? "ORG-MAP" : code,
                    Display = location.Name ?? "Mapped Organization Location"
                });
            }
        }
    }

    private static void EnsureExtensionUrls(DomainResource resource, List<string> extensionUrls)
    {
        foreach (var url in extensionUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal))
            AddTopLevelExtensionIfMissing(resource, url, new FhirString("normalization-opportunity"));
    }

    private static void EnsureSourcePathOpportunity(Resource resource, string? sourceFhirPath)
    {
        if (string.IsNullOrWhiteSpace(sourceFhirPath))
            return;

        var path = sourceFhirPath.Trim();

        if (string.Equals(path, "identifier.value", StringComparison.OrdinalIgnoreCase))
        {
            EnsureIdentifierValue(resource);
            return;
        }

        if (string.Equals(path, "code.coding.code", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "code.coding.system", StringComparison.OrdinalIgnoreCase))
        {
            EnsureCodeCoding(resource, "http://example.org/fhir/sid/opportunity", "opportunity-code");
        }
    }

    private static void EnsureIdentifierValue(Resource resource)
    {
        if (resource is Location location)
        {
            AddLocationCopyOperationOpportunities(location);
            return;
        }

        var identifierProp = resource.GetType().GetProperty("Identifier");
        if (identifierProp == null)
            return;

        if (identifierProp.GetValue(resource) is not List<Identifier> identifiers)
        {
            identifiers = [];
            identifierProp.SetValue(resource, identifiers);
        }

        var hasUsable = identifiers.Any(i => !string.IsNullOrWhiteSpace(i.Value));
        if (hasUsable)
            return;

        identifiers.Add(new Identifier
        {
            System = "http://example.org/fhir/sid/identifier",
            Value = string.IsNullOrWhiteSpace(resource.Id) ? Guid.NewGuid().ToString("N") : resource.Id
        });
    }

    private static void EnsureCodeMapOpportunity(Resource resource, GenerationRequirement requirement)
    {
        var map = requirement.CodeSystemMaps.FirstOrDefault();
        var sourceSystem = map?.SourceSystem;
        var sourceCode = map?.SourceCodes.Keys.FirstOrDefault() ?? "opportunity-code";
        if (string.IsNullOrWhiteSpace(sourceSystem))
            sourceSystem = "http://example.org/fhir/sid/opportunity";

        var path = string.IsNullOrWhiteSpace(requirement.CodeMapFhirPath)
            ? "code.coding.code"
            : requirement.CodeMapFhirPath.Trim();

        if (string.Equals(path, "code.coding.code", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "code.coding.system", StringComparison.OrdinalIgnoreCase))
        {
            EnsureCodeCoding(resource, sourceSystem, sourceCode);
        }
    }

    private static void EnsureConditionalOpportunity(Resource resource, GenerationRequirement requirement)
    {
        foreach (var condition in requirement.Conditions)
        {
            var op = condition.Operator?.Trim() ?? string.Empty;
            if (op.Equals("Exists", StringComparison.OrdinalIgnoreCase)
                || op.Equals("Equal", StringComparison.OrdinalIgnoreCase)
                || op.Equals("GreaterThan", StringComparison.OrdinalIgnoreCase)
                || op.Equals("GreaterThanOrEqual", StringComparison.OrdinalIgnoreCase)
                || op.Equals("LessThan", StringComparison.OrdinalIgnoreCase)
                || op.Equals("LessThanOrEqual", StringComparison.OrdinalIgnoreCase))
            {
                EnsureSourcePathOpportunity(resource, condition.FhirPathSource);
            }
        }
    }

    private static void EnsureCodeCoding(Resource resource, string system, string code)
    {
        var codeProp = resource.GetType().GetProperty("Code");
        if (codeProp == null)
            return;

        if (codeProp.GetValue(resource) is not CodeableConcept concept)
        {
            concept = new CodeableConcept();
            codeProp.SetValue(resource, concept);
        }

        concept.Coding ??= [];
        if (concept.Coding.Any(c => string.Equals(c.System, system, StringComparison.Ordinal) && string.Equals(c.Code, code, StringComparison.Ordinal)))
            return;

        concept.Coding.Add(new Coding(system, code));
    }
}
