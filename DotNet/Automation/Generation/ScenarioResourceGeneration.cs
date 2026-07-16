using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Shared scenario-driven FHIR resource generation logic, used by both
/// <see cref="FhirBundleGenerator"/> (single-pass bulk bundle assembly) and
/// <see cref="FhirGenerationPipeline"/> (streaming per-patient generation + upload).
///
/// Centralises:
/// <list type="bullet">
///   <item>Bundle entry construction + serialization helpers (<see cref="Entry"/>, <see cref="Serialize"/>).</item>
///   <item>The shared-infrastructure block — Organization, 5 Locations, 3 Devices,
///     all Practitioners, and the formulary Medications — built once per run via
///     <see cref="BuildSharedInfrastructure"/>.</item>
///   <item>The scenario-driven per-patient resource fan-out
///     (<see cref="GenerateScenarioDrivenResources"/>) including scenario-appropriate
///     code/value selection and the natural reference chains
///     (ServiceRequest → Specimen → Observation → DiagnosticReport,
///     MedicationRequest → Medication, MedicationAdministration → MedicationRequest, etc.).</item>
/// </list>
///
/// Anything tied to a specific consumer's orchestration model — bulk bundle
/// chunking in <see cref="FhirBundleGenerator"/>, profile-driven encounter-class
/// branching and Hypoglycemic-qualifying medication pairs in
/// <see cref="FhirGenerationPipeline"/> — stays in the consumer.
///
/// Extracting this class removed roughly 250 lines of verbatim duplication
/// between the two consumers; both previously kept their own copy of every
/// generator below, drifting only in <c>SharedIds</c> qualification and access
/// modifiers. Future scenario additions land in exactly one place.
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
    //  Resource-type distribution
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolves the (resourceType, fraction) distribution from the supplied
    /// <see cref="FhirGenerationConfig"/>, falling back to the default
    /// distribution when none is supplied. Order is preserved so callers that
    /// rely on resource-creation order (reference chains require certain
    /// resources to exist before their referrers) get stable behaviour.
    /// </summary>
    internal static (string ResourceType, double Fraction)[] ResolveDistribution(FhirGenerationConfig? config)
    {
        var dict = (config ?? new FhirGenerationConfig()).ResourceDistribution;
        return dict.Select(kv => (kv.Key, kv.Value)).ToArray();
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
    //  Per-patient scenario-driven fan-out
    // ------------------------------------------------------------------

    /// <summary>
    /// Generates all bulk resources for a single patient using scenario-driven
    /// resource selection. Resources are picked from clinically appropriate
    /// subsets of the global pools so a pneumonia patient gets antibiotics and
    /// chest X-rays, not insulin and echocardiograms.
    ///
    /// Also builds natural clinical reference chains during generation:
    /// ServiceRequest → Specimen → Observation → DiagnosticReport,
    /// MedicationRequest → Medication, MedicationAdministration → MedicationRequest.
    ///
    /// Effective-date stamping spreads each resource evenly across
    /// <c>[encStart, encEnd]</c>; bound the encounter window inside any clinical
    /// period (via <see cref="FhirBundleGenerator.DeriveInpatientEncounterWindow"/>
    /// or <see cref="FhirBundleGenerator.DeriveOutpatientEncounterWindow"/>) before
    /// calling, otherwise late-encounter resources can spill past a downstream
    /// consumer's date filter.
    /// </summary>
    internal static void GenerateScenarioDrivenResources(
        List<Bundle.EntryComponent> entries,
        int scenarioIdx,
        string patientId,
        string encounterId,
        DateTime encStart,
        DateTime encEnd,
        string primaryDxId,
        string attendingPractId,
        string careTeamId,
        int totalResourcesPerPatient,
        int baseSeed,
        int patientOrdinal,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        // Build scenario-appropriate index subsets
        var medIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalMedicationIndices, ScenarioResourceMap.ScenarioMedicationIndices,
            scenarioIdx, FhirGenerationCodes.Medications.Length);
        var obsIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalObservationIndices, ScenarioResourceMap.ScenarioObservationIndices,
            scenarioIdx, FhirGenerationCodes.Observations.Length);
        var procIndices = ScenarioResourceMap.ScenarioProcedureIndices[
            FhirBundleGenerator.Mod(scenarioIdx, ScenarioResourceMap.ScenarioProcedureIndices.Length)];
        var specIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalSpecimenIndices, ScenarioResourceMap.ScenarioSpecimenIndices,
            scenarioIdx, FhirGenerationCodes.Specimens.Length);
        var imgIndices = ScenarioResourceMap.ScenarioImagingIndices[
            FhirBundleGenerator.Mod(scenarioIdx, ScenarioResourceMap.ScenarioImagingIndices.Length)];
        var srIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalServiceRequestIndices, ScenarioResourceMap.ScenarioServiceRequestIndices,
            scenarioIdx, FhirGenerationCodes.ServiceRequests.Length);
        var condIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalConditionIndices, ScenarioResourceMap.ScenarioConditionIndices,
            scenarioIdx, FhirGenerationCodes.Conditions.Length);

        var medicationRequestIds = new List<string>();
        var specimenIds = new List<string>();
        var observationIds = new List<string>();
        var conditionIds = new List<string> { primaryDxId };
        var serviceRequestIds = new List<string>();
        var diagnosticReportIds = new List<string>();
        var resourceIndex = 0;
        var distribution = ResolveDistribution(config);
        var includeLowValueOptionalReferences = config?.IncludeLowValueOptionalReferences ?? true;

        foreach (var (resourceType, fraction) in distribution)
        {
            var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

            for (var i = 0; i < count; i++)
            {
                resourceIndex++;
                var seed = baseSeed + (patientOrdinal * 31 + i);
                var resourceId = $"{patientId}-{FhirBundleGenerator.AbbreviateResourceType(resourceType)}-{resourceIndex:D3}";
                var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                var effectiveDate = encStart.Add(offset);
                var practId = sharedPractitionerIds[FhirBundleGenerator.Mod(seed, sharedPractitionerIds.Count)];

                Resource resource = resourceType switch
                {
                    "Observation" => GenerateScenarioObservation(resourceId, patientId, encounterId, effectiveDate, seed, obsIndices, specimenIds, observationIds, ids),
                    "Condition" => GenerateScenarioCondition(resourceId, patientId, encounterId, effectiveDate, encEnd, seed, condIndices, conditionIds),
                    "Procedure" => GenerateScenarioProcedure(resourceId, patientId, encounterId, effectiveDate, seed, practId, procIndices, conditionIds, ids),
                    "MedicationRequest" => GenerateScenarioMedicationRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, medIndices, conditionIds, sharedMedicationIds, medicationRequestIds),
                    "MedicationAdministration" => GenerateScenarioMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, seed, medIndices, sharedMedicationIds, medicationRequestIds, practId, includeLowValueOptionalReferences),
                    "DiagnosticReport" => GenerateScenarioDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, seed, observationIds, specimenIds, practId, diagnosticReportIds),
                    "ServiceRequest" => GenerateScenarioServiceRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, srIndices, conditionIds, serviceRequestIds, ids),
                    "Coverage" => CoverageFactory.Generate(resourceId, patientId, encStart, encEnd, seed),
                    "Specimen" => GenerateScenarioSpecimen(resourceId, patientId, effectiveDate, seed, specIndices, specimenIds, practId),
                    "AllergyIntolerance" => AllergyIntoleranceFactory.Generate(resourceId, patientId, encStart, seed, practId),
                    "Immunization" => ImmunizationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.HospitalLocation, ids.Organization),
                    "ImagingStudy" => GenerateScenarioImagingStudy(resourceId, patientId, encounterId, effectiveDate, seed, imgIndices, serviceRequestIds, practId, includeLowValueOptionalReferences, ids),
                    "CareTeam" => CareTeamFactory.Generate(resourceId, patientId, encounterId, attendingPractId, effectiveDate, ids.Organization),
                    "CarePlan" => CarePlanFactory.Generate(resourceId, patientId, encounterId, careTeamId, effectiveDate, seed),
                    "DocumentReference" => DocumentReferenceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.Organization, attendingPractId),
                    "Provenance" => GenerateScenarioProvenance(resourceId, patientId, encounterId, effectiveDate, practId, diagnosticReportIds, includeLowValueOptionalReferences, ids),
                    _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                };

                entries.Add(Entry($"{resourceType}/{resourceId}", resource));
            }
        }

        var listId = $"SyntheticList-{patientId}";
        entries.Add(Entry($"List/{listId}",
            CensusListFactory.Generate(listId, patientId, encStart)));
    }

    // ------------------------------------------------------------------
    //  Scenario-aware resource generators — pick from scenario subsets
    //  and wire up reference chains during creation
    // ------------------------------------------------------------------

    private static Observation GenerateScenarioObservation(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] obsIndices, List<string> specimenIds, List<string> observationIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(obsIndices, seed, FhirGenerationCodes.Observations.Length);
        var v = FhirGenerationCodes.Observations[poolIdx];
        observationIds.Add(id);
        return ObservationFactory.Create(id, patientId, encounterId, effective,
            v.Code, v.Display, v.Category, v.Unit,
            v.CritLow, v.NormLow, v.NormHigh, v.CritHigh, seed, specimenIds, ids.Organization);
    }

    private static Condition GenerateScenarioCondition(
        string id, string patientId, string encounterId, DateTime onset, DateTime abatement, int seed,
        int[] condIndices, List<string> conditionIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(condIndices, seed, FhirGenerationCodes.Conditions.Length);
        var v = FhirGenerationCodes.Conditions[poolIdx];
        conditionIds.Add(id);
        return ConditionFactory.Create(id, patientId, encounterId, onset, abatement, seed,
            v.Code, v.Display, v.IcdCode, v.Category);
    }

    private static Procedure GenerateScenarioProcedure(
        string id, string patientId, string encounterId, DateTime performed, int seed, string practId,
        int[] procIndices, List<string> conditionIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(procIndices, seed, FhirGenerationCodes.Procedures.Length);
        var v = FhirGenerationCodes.Procedures[poolIdx];
        return ProcedureFactory.Create(id, patientId, encounterId, performed, seed, practId,
            ids.HospitalLocation, ids.Organization,
            v.Code, v.Display, v.BodySiteCode, v.BodySiteDisplay,
            v.OutcomeCode, v.OutcomeDisplay,
            conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null);
    }

    private static MedicationRequest GenerateScenarioMedicationRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] medIndices, List<string> conditionIds, List<string> sharedMedicationIds, List<string> medicationRequestIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        // Reference the shared formulary Medication that matches this drug.
        var medicationRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var req = MedicationRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.FreqPerDay, v.Prn,
            v.IndicationSnomed, v.IndicationDisplay, reasonConditionId, medicationRefId);
        medicationRequestIds.Add(id);
        return req;
    }

    private static MedicationAdministration GenerateScenarioMedicationAdministration(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] medIndices, List<string> sharedMedicationIds, List<string> medicationRequestIds, string practId,
        bool includeLowValueOptionalReferences)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        // Reference the shared formulary Medication that matches this drug.
        var medRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var isIv = v.RouteCode == "47625008";
        var admin = MedicationAdministrationFactory.Create(id, patientId, encounterId, effective, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.IndicationSnomed, v.IndicationDisplay, isIv, medRefId);
        // Optional link: MedicationAdministration.request → MedicationRequest
        if (includeLowValueOptionalReferences && medicationRequestIds.Count > 0)
            admin.Request = new ResourceReference($"MedicationRequest/{medicationRequestIds[seed % medicationRequestIds.Count]}");
        return admin;
    }

    private static Specimen GenerateScenarioSpecimen(
        string id, string patientId, DateTime collected, int seed,
        int[] specIndices, List<string> specimenIds, string practId)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(specIndices, seed, FhirGenerationCodes.Specimens.Length);
        specimenIds.Add(id);
        var v = FhirGenerationCodes.Specimens[poolIdx];
        return SpecimenFactory.Create(id, patientId, collected, seed,
            v.TypeCode, v.TypeDisplay, v.TypeSystem,
            v.ContainerCode, v.ContainerDisplay,
            v.CollectionMethod, v.BodySiteCode, v.BodySiteDisplay, practId);
    }

    private static DiagnosticReport GenerateScenarioDiagnosticReport(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        List<string> observationIds, List<string> specimenIds, string practId,
        List<string> diagnosticReportIds)
    {
        var report = DiagnosticReportFactory.Generate(id, patientId, encounterId, effective, seed,
            observationIds, specimenIds, practId);
        diagnosticReportIds.Add(id);
        return report;
    }

    private static ServiceRequest GenerateScenarioServiceRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] srIndices, List<string> conditionIds, List<string> serviceRequestIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(srIndices, seed, FhirGenerationCodes.ServiceRequests.Length);
        var v = FhirGenerationCodes.ServiceRequests[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var sr = ServiceRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.Code, v.Display, v.IsLab, v.System, reasonConditionId, ids.Organization);
        serviceRequestIds.Add(id);
        return sr;
    }

    private static ImagingStudy GenerateScenarioImagingStudy(
        string id, string patientId, string encounterId, DateTime started, int seed,
        int[] imgIndices, List<string> serviceRequestIds, string practId,
        bool includeLowValueOptionalReferences, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(imgIndices, seed, FhirGenerationCodes.ImagingStudies.Length);
        var v = FhirGenerationCodes.ImagingStudies[poolIdx];
        var study = ImagingStudyFactory.Create(id, patientId, encounterId, started, ids.HospitalLocation, practId,
            v.SnomedCode, v.Display, v.Modality,
            v.BodySiteCode, v.BodySiteDisplay, v.ReasonCode, v.ReasonDisplay);
        // Optional link: ImagingStudy.basedOn → ServiceRequest
        if (includeLowValueOptionalReferences && serviceRequestIds.Count > 0)
        {
            study.BasedOn ??= [];
            study.BasedOn.Add(new ResourceReference($"ServiceRequest/{serviceRequestIds[seed % serviceRequestIds.Count]}"));
        }
        return study;
    }

    private static Provenance GenerateScenarioProvenance(
        string id, string patientId, string encounterId, DateTime recorded, string practId,
        List<string> diagnosticReportIds,
        bool includeLowValueOptionalReferences, FhirBundleGenerator.SharedIds ids)
    {
        var prov = ProvenanceFactory.Create(id, patientId, encounterId, recorded, practId, ids.Organization);
        // Optional link: Provenance.target to DiagnosticReport
        if (includeLowValueOptionalReferences && diagnosticReportIds.Count > 0)
        {
            prov.Target ??= [];
            prov.Target.Add(new ResourceReference($"DiagnosticReport/{diagnosticReportIds[^1]}"));
        }
        return prov;
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
    /// Emits the per-patient core anchor entries shared by both the bulk
    /// (<see cref="FhirBundleGenerator"/>) and streaming
    /// (<see cref="FhirGenerationPipeline"/>) paths, in their canonical order
    /// (Patient → Device → primary Condition → Encounter → CareTeam → CarePlan
    /// → optional Hypoglycemic-qualifying medication pair → scenario fan-out).
    ///
    /// The Encounter is built by the caller and passed in because each path
    /// has its own encounter variants (bulk uses the standard inpatient form;
    /// the pipeline switches between standard inpatient, Hypoglycemic-flavoured
    /// inpatient, and ambulatory based on the patient profile).
    /// </summary>
    internal static void AddPatientCoreAndScenarioResources(
        List<Bundle.EntryComponent> entries,
        string patientId,
        int patientSeed,
        int patientIndex,
        int baseSeed,
        int totalResourcesPerPatient,
        DateTime encStart,
        DateTime encEnd,
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        PatientAnchorContext anchors,
        Resource encounter,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids,
        GenerationRequirementsPlan? generationRequirementsPlan = null,
        bool addHypoglycemicMedicationPair = false)
    {
        // Core anchors — order matters: Patient → Device → primary Condition →
        // Encounter → CareTeam → CarePlan → (optional hypo meds) → scenario fan-out.
        var patient = PatientFactory.Generate(patientId, patientSeed, anchors.GpPractId);
        patient.ManagingOrganization = new ResourceReference($"Organization/{ids.Organization}", "General Test Hospital");
        entries.Add(Entry($"Patient/{patientId}", patient));

        entries.Add(Entry($"Device/{anchors.PatientDeviceId}",
            DeviceFactory.Generate(anchors.PatientDeviceId, patientSeed, patientId)));

        entries.Add(Entry($"Condition/{anchors.PrimaryDxId}",
            ConditionFactory.CreatePrimary(
                anchors.PrimaryDxId, patientId, anchors.EncounterId, encStart,
                scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));

        entries.Add(Entry($"Encounter/{anchors.EncounterId}", encounter));

        entries.Add(Entry($"CareTeam/{anchors.CareTeamId}",
            CareTeamFactory.Generate(anchors.CareTeamId, patientId, anchors.EncounterId,
                anchors.AttendingPractId, encStart, ids.Organization)));

        entries.Add(Entry($"CarePlan/{anchors.CarePlanId}",
            CarePlanFactory.Generate(anchors.CarePlanId, patientId, anchors.EncounterId,
                anchors.CareTeamId, encStart, patientSeed)));

        if (addHypoglycemicMedicationPair)
        {
            AddHypoglycemicQualifyingMedicationEntries(entries, patientId, anchors.EncounterId,
                anchors.AttendingPractId, patientSeed, encStart, ids);
        }

        var scenarioIdx = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
        GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, anchors.EncounterId,
            encStart, encEnd, anchors.PrimaryDxId, anchors.AttendingPractId, anchors.CareTeamId,
            totalResourcesPerPatient, baseSeed, patientIndex,
            sharedPractitionerIds, sharedMedicationIds, config, ids);

        ApplyGenerationRequirements(entries, generationRequirementsPlan);
    }

    /// <summary>
    /// Emits the Hypoglycemic-measure-qualifying MedicationRequest +
    /// MedicationAdministration pair (insulin glargine, RxNorm 274783) that
    /// references the run-scoped shared <c>HypoInsulinGlargineMedication</c>.
    /// Only the streaming path uses this today (driven by
    /// <c>PatientProfile.RequiresHypoglycemicMedication</c>) but it lives here
    /// so any future caller can opt in via
    /// <see cref="AddPatientCoreAndScenarioResources"/>.
    /// </summary>
    internal static void AddHypoglycemicQualifyingMedicationEntries(
        List<Bundle.EntryComponent> entries,
        string patientId,
        string encounterId,
        string practitionerId,
        int seed,
        DateTime encounterStart,
        FhirBundleGenerator.SharedIds ids)
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

    private static void ApplyGenerationRequirements(
        List<Bundle.EntryComponent> entries,
        GenerationRequirementsPlan? generationRequirementsPlan)
    {
        if (generationRequirementsPlan == null || generationRequirementsPlan.Requirements.Count == 0)
            return;

        var resourcesByType = entries
            .Where(e => e.Resource != null)
            .GroupBy(e => e.Resource!.TypeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Resource!).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in generationRequirementsPlan.Requirements)
        {
            foreach (var resourceType in requirement.ResourceTypes.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!resourcesByType.TryGetValue(resourceType, out var candidates) || candidates.Count == 0)
                    continue;

                ApplyRequirement(requirement, candidates[0]);
            }
        }
    }

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
