using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class FhirBundleGeneratorTests
{
    private static readonly HashSet<string> AllowedDiagnosticReportCategories =
    ["HM", "CH", "UA", "MB", "RAD", "PT"];

    private static readonly HashSet<string> AllowedDiagnosticReportLoincCategoryCodes =
    ["LP7796-8", "LP29708-2", "LP29684-5"];

    // ----------------------------------------------------------------------
    //  Core Generate(...) behavior
    // ----------------------------------------------------------------------

    [Fact]
    public void Generate_ReturnsRequestedPatientCountWithStableIds()
    {
        var output = new NullOutputHelper();

        var (patientIds, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 2, totalResourcesPerPatient: 120);

        Assert.Equal(2, patientIds.Count);

        // Patient IDs follow "Patient-{RunTag}-{ordinal:D3}". The ordinal is stable;
        // RunTag is a per-run 8-char hex tag that makes them collision-safe across
        // concurrent test runs.
        Assert.All(patientIds, id =>
        {
            var parts = id.Split('-');
            Assert.Equal(3, parts.Length);
            Assert.Equal("Patient", parts[0]);
            Assert.Equal(8, parts[1].Length);
        });
        Assert.EndsWith("-001", patientIds[0]);
        Assert.EndsWith("-002", patientIds[1]);
        Assert.NotEmpty(bundles);
    }

    [Fact]
    public void Generate_ProducesTransactionBundlesWithPutRequestsAndMax500Entries()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 2500);

        Assert.True(bundles.Count > 1, "Expected multiple bundles when chunking above 500 entries.");

        foreach (var (name, json) in bundles)
        {
            Assert.Contains("_chunk", name);
            var bundle = ParseBundle(json);
            Assert.Equal(Bundle.BundleType.Transaction, bundle.Type);
            Assert.NotNull(bundle.Entry);
            Assert.True(bundle.Entry.Count <= 500, $"Bundle {name} has {bundle.Entry.Count} entries.");

            foreach (var entry in bundle.Entry)
            {
                Assert.NotNull(entry.Request);
                Assert.Equal(Bundle.HTTPVerb.PUT, entry.Request.Method);
                Assert.False(string.IsNullOrWhiteSpace(entry.Request.Url));
                Assert.NotNull(entry.Resource);
            }
        }
    }

    [Fact]
    public void Generate_FirstBundleContainsSharedInfrastructure()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 100);
        var first = ParseBundle(bundles[0].Json);

        // Shared infrastructure IDs are run-tag-scoped: "{RunTag}-{Kind}".
        // Verify each expected kind exists by URL shape rather than by hardcoded legacy ids.
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Organization/") && e.Request.Url.EndsWith("-Org-Hospital"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-Hospital"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-ICU"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-ED"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-StepDown"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-Outpatient"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-PulseOx"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-Ventilator"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-CPAP"));
        Assert.Contains(first.Entry, e => e.Request.Url!.StartsWith("Practitioner/"));
    }

    [Fact]
    public void Generate_CreatesCoherentPrimaryDiagnosisAndEncounterLink()
    {
        var output = new NullOutputHelper();

        var (patientIds, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();
        var resources = FlattenResources(bundles);

        var encounter = resources.Values.OfType<Encounter>().Single(e => e.Id == $"{patientId}-Enc-001");
        Assert.NotNull(encounter.Diagnosis);
        Assert.NotEmpty(encounter.Diagnosis);

        var diagnosisRef = encounter.Diagnosis.First().Condition?.Reference;
        Assert.False(string.IsNullOrWhiteSpace(diagnosisRef));
        Assert.True(resources.ContainsKey(diagnosisRef!), $"Missing encounter diagnosis reference: {diagnosisRef}");

        var primaryCondition = Assert.IsType<Condition>(resources[diagnosisRef!]);
        Assert.Equal(patientId, primaryCondition.Subject?.Reference?.Split('/').Last());
        Assert.Equal($"Encounter/{patientId}-Enc-001", primaryCondition.Encounter?.Reference);
    }

    [Fact]
    public void Generate_SpecimenCollectorUsesPractitionerReference()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 150);
        var resources = FlattenResources(bundles);

        var specimens = resources.Values.OfType<Specimen>().ToList();
        Assert.NotEmpty(specimens);

        foreach (var specimen in specimens)
        {
            var collectorRef = specimen.Collection?.Collector?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(collectorRef));
            Assert.StartsWith("Practitioner/", collectorRef);
            Assert.True(resources.ContainsKey(collectorRef!), $"Collector reference not found: {collectorRef}");
        }
    }

    [Fact]
    public void Generate_DiagnosticReportsUseValidCategoryCoding()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 200);
        var resources = FlattenResources(bundles);

        var reports = resources.Values.OfType<DiagnosticReport>().ToList();
        Assert.NotEmpty(reports);

        foreach (var report in reports)
        {
            var coding = report.Category?.FirstOrDefault()?.Coding?.FirstOrDefault();
            Assert.NotNull(coding);
            AssertAllowedDiagnosticReportCategory(coding!);
        }
    }

    [Fact]
    public void Generate_ObservationAndDiagnosticReportReferencesResolve()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 220);
        var resources = FlattenResources(bundles);

        foreach (var obs in resources.Values.OfType<Observation>())
        {
            var encounterRef = obs.Encounter?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(encounterRef));
            Assert.True(resources.ContainsKey(encounterRef!));

            var subjectRef = obs.Subject?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(subjectRef));
            Assert.True(resources.ContainsKey(subjectRef!));

            if (obs.Specimen != null)
                Assert.True(resources.ContainsKey(obs.Specimen.Reference!), $"Missing specimen reference: {obs.Specimen.Reference}");
        }

        foreach (var report in resources.Values.OfType<DiagnosticReport>())
        {
            foreach (var result in report.Result)
                Assert.True(resources.ContainsKey(result.Reference!), $"Missing report.result reference: {result.Reference}");

            foreach (var specimen in report.Specimen)
                Assert.True(resources.ContainsKey(specimen.Reference!), $"Missing report.specimen reference: {specimen.Reference}");
        }
    }

    [Fact]
    public void Generate_IsStructurallyDeterministicForSameInput()
    {
        // The generator is seed-deterministic for clinical content but uses a per-run
        // GUID tag for shared infrastructure IDs (collision safety across concurrent test
        // runs against the same FHIR server). Byte-for-byte JSON equality is therefore not
        // expected — but the *shape* of the output must be identical.
        var output = new NullOutputHelper();

        var run1 = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 180);
        var run2 = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 180);

        Assert.Equal(run1.PatientIds.Count, run2.PatientIds.Count);
        Assert.Equal(run1.Bundles.Count, run2.Bundles.Count);

        static Dictionary<string, int> ResourceTypeCounts(List<(string Name, string Json)> bundles)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in FlattenEntries(bundles))
            {
                var t = entry.Resource.TypeName;
                counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
            }
            return counts;
        }

        var counts1 = ResourceTypeCounts(run1.Bundles);
        var counts2 = ResourceTypeCounts(run2.Bundles);
        Assert.Equal(counts1.OrderBy(k => k.Key), counts2.OrderBy(k => k.Key));
    }

    // ----------------------------------------------------------------------
    //  Full-structure integrity check for a large deterministic run
    // ----------------------------------------------------------------------

    [Fact]
    public void Generate_LargeDataset_HasRigorousInternalStructuralIntegrity()
    {
        var output = new NullOutputHelper();

        var (patientIds, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 4, totalResourcesPerPatient: 1200);

        Assert.Equal(4, patientIds.Count);
        Assert.True(bundles.Count > 4);

        var entries = FlattenEntries(bundles);
        Assert.NotEmpty(entries);

        // Transaction entry integrity
        foreach (var entry in entries)
        {
            Assert.NotNull(entry.Request);
            Assert.Equal(Bundle.HTTPVerb.PUT, entry.Request.Method);
            Assert.False(string.IsNullOrWhiteSpace(entry.Request.Url));
            Assert.False(string.IsNullOrWhiteSpace(entry.FullUrl));
            Assert.EndsWith(entry.Request.Url!, entry.FullUrl!, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(entry.Resource);
        }

        // No duplicated request URLs (resourceType/id)
        var duplicateUrls = entries
            .GroupBy(e => e.Request.Url!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicateUrls);

        var resources = entries.ToDictionary(e => e.Request.Url!, e => e.Resource!, StringComparer.OrdinalIgnoreCase);

        // Shared infrastructure existence (matched by suffix, not by legacy constants).
        Assert.Single(resources.Keys, k => k.StartsWith("Organization/") && k.EndsWith("-Org-Hospital"));
        Assert.Single(resources.Keys, k => k.StartsWith("Location/") && k.EndsWith("-Loc-Hospital"));
        Assert.Single(resources.Keys, k => k.StartsWith("Location/") && k.EndsWith("-Loc-ICU"));
        Assert.Single(resources.Keys, k => k.StartsWith("Location/") && k.EndsWith("-Loc-ED"));
        Assert.Single(resources.Keys, k => k.StartsWith("Location/") && k.EndsWith("-Loc-StepDown"));

        // Per-patient coherence validations
        foreach (var patientId in patientIds)
        {
            var patientKey = $"Patient/{patientId}";
            var encounterKey = $"Encounter/{patientId}-Enc-001";
            var primaryConditionKey = $"Condition/{patientId}-Condition-primary";
            var careTeamKey = $"CareTeam/{patientId}-CareTeam-001";
            var carePlanKey = $"CarePlan/{patientId}-CarePlan-001";
            var deviceKey = $"Device/{patientId}-Device-001";
            var listKey = $"List/SyntheticList-{patientId}";

            Assert.True(resources.ContainsKey(patientKey), $"Missing {patientKey}");
            Assert.True(resources.ContainsKey(encounterKey), $"Missing {encounterKey}");
            Assert.True(resources.ContainsKey(primaryConditionKey), $"Missing {primaryConditionKey}");
            Assert.True(resources.ContainsKey(careTeamKey), $"Missing {careTeamKey}");
            Assert.True(resources.ContainsKey(carePlanKey), $"Missing {carePlanKey}");
            Assert.True(resources.ContainsKey(deviceKey), $"Missing {deviceKey}");
            Assert.True(resources.ContainsKey(listKey), $"Missing {listKey}");

            var patient = Assert.IsType<Patient>(resources[patientKey]);
            Assert.False(string.IsNullOrWhiteSpace(patient.BirthDate));
            Assert.NotNull(patient.Name);
            Assert.NotEmpty(patient.Name);
            Assert.NotEmpty(patient.Identifier);

            var encounter = Assert.IsType<Encounter>(resources[encounterKey]);
            Assert.Equal(Encounter.EncounterStatus.Finished, encounter.Status);
            Assert.Equal(patientKey, encounter.Subject?.Reference);
            Assert.NotNull(encounter.Class);
            Assert.NotNull(encounter.Period);
            Assert.False(string.IsNullOrWhiteSpace(encounter.Period.Start));
            Assert.False(string.IsNullOrWhiteSpace(encounter.Period.End));
            Assert.NotNull(encounter.ServiceProvider);
            AssertRefExists(resources, encounter.ServiceProvider, "Encounter.ServiceProvider");
            Assert.NotEmpty(encounter.Location);
            Assert.NotEmpty(encounter.Diagnosis);
            AssertRefExists(resources, encounter.Diagnosis.First().Condition, "Encounter.Diagnosis.Condition");

            var primaryCondition = Assert.IsType<Condition>(resources[primaryConditionKey]);
            Assert.Equal(patientKey, primaryCondition.Subject?.Reference);
            Assert.Equal(encounterKey, primaryCondition.Encounter?.Reference);

            var device = Assert.IsType<Device>(resources[deviceKey]);
            Assert.Equal(patientKey, device.Patient?.Reference);

            var censusList = Assert.IsType<List>(resources[listKey]);
            Assert.Equal(List.ListStatus.Current, censusList.Status);
            Assert.Equal(ListMode.Working, censusList.Mode);
            Assert.NotEmpty(censusList.Entry);
            Assert.Equal(patientKey, censusList.Entry.First().Item?.Reference);
        }

        // Type-specific required-field and reference validations for every generated resource
        foreach (var (key, resource) in resources)
        {
            Assert.False(string.IsNullOrWhiteSpace(resource.Id));
            AssertResourceTypeInvariants(key, resource, resources);
        }
    }

    // ----------------------------------------------------------------------
    //  CQL filter simulator (data-driven, no generator coupling)
    // ----------------------------------------------------------------------

    [Fact]
    public void CqlFilterSimulator_AchProfile_ExcludesOnlyConditionsFailingSdeRules()
    {
        var encounterId = "TestPatient-Enc-001";
        var encStart = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var encEnd = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        var includedProblemList = new CqlFilterSimulator.ConditionContext(
            "TestPatient-Condition-001",
            IsActive: true,
            RecordedDate: encStart.Date,             // strictly before encEnd.Date
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        var excludedResolvedProblemList = includedProblemList with
        {
            ResourceId = "TestPatient-Condition-002",
            IsActive = false
        };

        var includedEncounterDiagnosis = new CqlFilterSimulator.ConditionContext(
            "TestPatient-Condition-003",
            IsActive: false, // status does not matter on this branch
            RecordedDate: encEnd.Date,
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["encounter-diagnosis"]);

        var excludedUnlinkedEncounterDiagnosis = includedEncounterDiagnosis with
        {
            ResourceId = "TestPatient-Condition-004",
            EncounterReference = "Encounter/OtherEnc"
        };

        var input = new CqlFilterSimulator.PatientCqlInput(
            PatientId: "TestPatient",
            EncounterId: encounterId,
            EncounterStart: encStart,
            EncounterEnd: encEnd,
            Conditions:
            [
                includedProblemList,
                excludedResolvedProblemList,
                includedEncounterDiagnosis,
                excludedUnlinkedEncounterDiagnosis
            ],
            Observations: []);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            input);

        Assert.Contains("Condition/TestPatient-Condition-002", excluded);
        Assert.Contains("Condition/TestPatient-Condition-004", excluded);
        Assert.DoesNotContain("Condition/TestPatient-Condition-001", excluded);
        Assert.DoesNotContain("Condition/TestPatient-Condition-003", excluded);
    }

    [Fact]
    public void CqlFilterSimulator_NoMeasuresOrNoProfiles_ReturnsEmpty()
    {
        var input = new CqlFilterSimulator.PatientCqlInput(
            "P", "P-Enc-001",
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            [],
            []);

        Assert.Empty(CqlFilterSimulator.ComputeFilteredKeys([], input));
    }

    [Fact]
    public void CqlFilterSimulator_MultiMeasure_IntersectsExclusions()
    {
        // MeasureEval evaluates each measure independently; PatientAggregator unions the
        // contained resources across the per-measure .mr files. So a resource is only truly
        // absent from ABS when EVERY applicable measure excludes it.
        //
        // ACH requires active + recordedDate < encounterEnd for problem-list items.
        // Hypoglycemic only requires recordedDate <= encounterEnd.
        // A resolved (non-active) problem-list Condition recorded during the encounter is
        // excluded by ACH but INCLUDED by Hypoglycemic -> must NOT appear in the excluded set.

        var encounterId = "P-Enc-001";
        var encStart = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var encEnd = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        var resolvedProblemDuringEncounter = new CqlFilterSimulator.ConditionContext(
            "P-Condition-001",
            IsActive: false,
            RecordedDate: encStart.Date,
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        // This one is excluded by BOTH profiles: resolved problem-list-item recorded AFTER
        // the encounter fails ACH (not active, date not strictly before end) AND fails
        // Hypoglycemic (recordedDate > encounterEnd).
        var resolvedProblemAfterEncounter = new CqlFilterSimulator.ConditionContext(
            "P-Condition-002",
            IsActive: false,
            RecordedDate: encEnd.Date.AddDays(5),
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        var input = new CqlFilterSimulator.PatientCqlInput(
            PatientId: "P",
            EncounterId: encounterId,
            EncounterStart: encStart,
            EncounterEnd: encEnd,
            Conditions: [resolvedProblemDuringEncounter, resolvedProblemAfterEncounter],
            Observations: []);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            ],
            input);

        // Only excluded if BOTH measures exclude it.
        Assert.DoesNotContain("Condition/P-Condition-001", excluded);
        Assert.Contains("Condition/P-Condition-002", excluded);
    }

    // ----------------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------------

    private static void AssertResourceTypeInvariants(
        string key,
        Resource resource,
        Dictionary<string, Resource> resources)
    {
        switch (resource)
        {
            case Organization org:
                Assert.True(org.Active);
                Assert.False(string.IsNullOrWhiteSpace(org.Name));
                Assert.NotEmpty(org.Identifier);
                break;

            case Location loc:
                Assert.Equal(Location.LocationStatus.Active, loc.Status);
                Assert.False(string.IsNullOrWhiteSpace(loc.Name));
                AssertRefExists(resources, loc.ManagingOrganization, $"{key}.ManagingOrganization");
                break;

            case Practitioner pract:
                Assert.True(pract.Active);
                Assert.NotEmpty(pract.Name);
                Assert.NotEmpty(pract.Identifier);
                break;

            case Patient p:
                Assert.True(p.Active == true);
                Assert.False(string.IsNullOrWhiteSpace(p.BirthDate));
                Assert.NotEmpty(p.Name);
                Assert.NotEmpty(p.Identifier);
                break;

            case Device d:
                Assert.Equal(Device.FHIRDeviceStatus.Active, d.Status);
                Assert.NotNull(d.Type);
                if (d.Patient != null)
                    AssertRefExists(resources, d.Patient, $"{key}.Patient");
                break;

            case Encounter e:
                Assert.Equal(Encounter.EncounterStatus.Finished, e.Status);
                AssertRefExists(resources, e.Subject, $"{key}.Subject");
                AssertRefExists(resources, e.ServiceProvider, $"{key}.ServiceProvider");
                foreach (var l in e.Location)
                    AssertRefExists(resources, l.Location, $"{key}.Location");
                foreach (var dgn in e.Diagnosis)
                    AssertRefExists(resources, dgn.Condition, $"{key}.Diagnosis.Condition");
                break;

            case Condition c:
                Assert.NotNull(c.ClinicalStatus);
                Assert.NotNull(c.VerificationStatus);
                Assert.NotNull(c.Code);
                AssertRefExists(resources, c.Subject, $"{key}.Subject");
                AssertRefExists(resources, c.Encounter, $"{key}.Encounter");
                break;

            case Observation o:
                Assert.Equal(ObservationStatus.Final, o.Status);
                Assert.NotNull(o.Code);
                AssertRefExists(resources, o.Subject, $"{key}.Subject");
                AssertRefExists(resources, o.Encounter, $"{key}.Encounter");
                Assert.True(o.Effective != null, $"{key}.Effective is required");
                if (o.Specimen != null)
                    AssertRefExists(resources, o.Specimen, $"{key}.Specimen");
                Assert.True(o.Value != null || (o.Component?.Count > 0), $"{key} has neither Value nor Component");
                break;

            case Procedure pr:
                Assert.Equal(EventStatus.Completed, pr.Status);
                Assert.NotNull(pr.Code);
                AssertRefExists(resources, pr.Subject, $"{key}.Subject");
                AssertRefExists(resources, pr.Encounter, $"{key}.Encounter");
                AssertRefExists(resources, pr.Location, $"{key}.Location");
                foreach (var rr in pr.ReasonReference)
                    AssertRefExists(resources, rr, $"{key}.ReasonReference");
                break;

            case MedicationRequest mr:
                Assert.NotNull(mr.Status);
                Assert.Equal(MedicationRequest.MedicationRequestIntent.Order, mr.Intent);
                Assert.True(mr.Medication != null, $"{key}.Medication required");
                AssertRefExists(resources, mr.Subject, $"{key}.Subject");
                AssertRefExists(resources, mr.Encounter, $"{key}.Encounter");
                AssertRefExists(resources, mr.Requester, $"{key}.Requester");
                foreach (var rr in mr.ReasonReference)
                    AssertRefExists(resources, rr, $"{key}.ReasonReference");
                break;

            case MedicationAdministration ma:
                Assert.Equal(MedicationAdministration.MedicationAdministrationStatusCodes.Completed, ma.Status);
                Assert.True(ma.Medication != null, $"{key}.Medication required");
                AssertRefExists(resources, ma.Subject, $"{key}.Subject");
                AssertRefExists(resources, ma.Context, $"{key}.Context");
                if (ma.Medication is ResourceReference medRef)
                    AssertRefExists(resources, medRef, $"{key}.MedicationReference");
                break;

            case Medication med:
                Assert.Equal(Medication.MedicationStatusCodes.Active, med.Status);
                Assert.NotNull(med.Code);
                Assert.NotEmpty(med.Ingredient);
                break;

            case DiagnosticReport dr:
                Assert.Equal(DiagnosticReport.DiagnosticReportStatus.Final, dr.Status);
                Assert.NotNull(dr.Code);
                AssertRefExists(resources, dr.Subject, $"{key}.Subject");
                AssertRefExists(resources, dr.Encounter, $"{key}.Encounter");
                Assert.NotEmpty(dr.Category);
                var coding = dr.Category.First().Coding?.FirstOrDefault();
                Assert.NotNull(coding);
                AssertAllowedDiagnosticReportCategory(coding!);
                foreach (var rr in dr.Result)
                    AssertRefExists(resources, rr, $"{key}.Result");
                foreach (var rr in dr.Specimen)
                    AssertRefExists(resources, rr, $"{key}.Specimen");
                break;

            case ServiceRequest sr:
                Assert.True(sr.Status is RequestStatus.Active or RequestStatus.Completed);
                Assert.Equal(RequestIntent.Order, sr.Intent);
                Assert.NotNull(sr.Code);
                AssertRefExists(resources, sr.Subject, $"{key}.Subject");
                AssertRefExists(resources, sr.Encounter, $"{key}.Encounter");
                AssertRefExists(resources, sr.Requester, $"{key}.Requester");
                foreach (var rr in sr.ReasonReference)
                    AssertRefExists(resources, rr, $"{key}.ReasonReference");
                break;

            case Coverage cv:
                Assert.Equal(FinancialResourceStatusCodes.Active, cv.Status);
                AssertRefExists(resources, cv.Beneficiary, $"{key}.Beneficiary");
                Assert.NotNull(cv.Type);
                Assert.NotEmpty(cv.Class);
                Assert.NotNull(cv.Class.First().Type);
                Assert.False(string.IsNullOrWhiteSpace(cv.Class.First().Value));
                break;

            case Specimen sp:
                Assert.Equal(Specimen.SpecimenStatus.Available, sp.Status);
                Assert.NotNull(sp.Type);
                AssertRefExists(resources, sp.Subject, $"{key}.Subject");
                Assert.NotNull(sp.Collection);
                AssertRefExists(resources, sp.Collection!.Collector, $"{key}.Collection.Collector");
                Assert.StartsWith("Practitioner/", sp.Collection!.Collector!.Reference!);
                break;

            case AllergyIntolerance ai:
                Assert.NotNull(ai.ClinicalStatus);
                Assert.NotNull(ai.VerificationStatus);
                Assert.NotNull(ai.Code);
                AssertRefExists(resources, ai.Patient, $"{key}.Patient");
                break;

            case Immunization im:
                Assert.Equal(Immunization.ImmunizationStatusCodes.Completed, im.Status);
                Assert.NotNull(im.VaccineCode);
                AssertRefExists(resources, im.Patient, $"{key}.Patient");
                AssertRefExists(resources, im.Encounter, $"{key}.Encounter");
                break;

            case ImagingStudy isr:
                Assert.Equal(ImagingStudy.ImagingStudyStatus.Available, isr.Status);
                AssertRefExists(resources, isr.Subject, $"{key}.Subject");
                AssertRefExists(resources, isr.Encounter, $"{key}.Encounter");
                Assert.NotEmpty(isr.Series);
                break;

            case CareTeam ct:
                Assert.Equal(CareTeam.CareTeamStatus.Active, ct.Status);
                AssertRefExists(resources, ct.Subject, $"{key}.Subject");
                AssertRefExists(resources, ct.Encounter, $"{key}.Encounter");
                Assert.NotEmpty(ct.Participant);
                break;

            case CarePlan cp:
                Assert.Equal(RequestStatus.Active, cp.Status);
                Assert.Equal(CarePlan.CarePlanIntent.Order, cp.Intent);
                AssertRefExists(resources, cp.Subject, $"{key}.Subject");
                AssertRefExists(resources, cp.Encounter, $"{key}.Encounter");
                foreach (var rr in cp.CareTeam)
                    AssertRefExists(resources, rr, $"{key}.CareTeam");
                Assert.NotEmpty(cp.Activity);
                break;

            case DocumentReference doc:
                Assert.Equal(DocumentReferenceStatus.Current, doc.Status);
                Assert.NotNull(doc.Type);
                AssertRefExists(resources, doc.Subject, $"{key}.Subject");
                Assert.NotEmpty(doc.Content);
                break;

            case Provenance prov:
                Assert.NotNull(prov.RecordedElement);
                Assert.NotEmpty(prov.Target);
                foreach (var rr in prov.Target)
                    AssertRefExists(resources, rr, $"{key}.Target");
                Assert.NotEmpty(prov.Agent);
                break;

            case List list:
                Assert.Equal(List.ListStatus.Current, list.Status);
                Assert.Equal(ListMode.Working, list.Mode);
                Assert.NotEmpty(list.Entry);
                AssertRefExists(resources, list.Entry.First().Item, $"{key}.Entry.Item");
                break;

            default:
                Assert.Fail($"Unexpected generated resource type: {resource.TypeName}");
                break;
        }
    }

    private static void AssertAllowedDiagnosticReportCategory(Coding coding)
    {
        if (string.Equals(coding.System, "http://terminology.hl7.org/CodeSystem/v2-0074", StringComparison.OrdinalIgnoreCase))
            Assert.Contains(coding.Code!, AllowedDiagnosticReportCategories);
        else
        {
            Assert.Equal("http://loinc.org", coding.System);
            Assert.Contains(coding.Code!, AllowedDiagnosticReportLoincCategoryCodes);
        }
    }

    private static Bundle ParseBundle(string json)
    {
        var bundle = JsonSerializer.Deserialize<Bundle>(json, LinkFhirSerializerOptions.ForFhirWithoutValidation());
        Assert.NotNull(bundle);
        return bundle!;
    }

    private static Dictionary<string, Resource> FlattenResources(List<(string Name, string Json)> bundles)
    {
        var map = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, json) in bundles)
        {
            var bundle = ParseBundle(json);
            foreach (var entry in bundle.Entry)
            {
                var key = entry.Request.Url!; // resourceType/id for PUT transaction entries
                map[key] = entry.Resource;
            }
        }

        return map;
    }

    private static List<Bundle.EntryComponent> FlattenEntries(List<(string Name, string Json)> bundles)
    {
        var entries = new List<Bundle.EntryComponent>();

        foreach (var (_, json) in bundles)
        {
            var bundle = ParseBundle(json);
            Assert.NotNull(bundle.Entry);
            entries.AddRange(bundle.Entry);
        }

        return entries;
    }

    private static void AssertRefExists(
        Dictionary<string, Resource> resources,
        ResourceReference? reference,
        string context)
    {
        Assert.NotNull(reference);
        Assert.False(string.IsNullOrWhiteSpace(reference!.Reference), $"{context} reference is null/empty.");

        var refValue = reference.Reference!;

        // Generated bundles use relative refs (ResourceType/id). Be defensive for absolute refs too.
        if (refValue.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var idx = refValue.IndexOf("/fhir/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                refValue = refValue[(idx + "/fhir/".Length)..];
        }

        Assert.True(resources.ContainsKey(refValue), $"{context} missing referenced resource: {refValue}");
    }

    private sealed class NullOutputHelper : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
