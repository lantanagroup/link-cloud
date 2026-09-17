using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ObservationFactory
{
    /// <summary>Generate an Observation from the pool using a seed index, tracking produced IDs.</summary>
    public static Observation Generate(
        string id,
        string patientId,
        string encounterId,
        DateTime effective,
        int seed,
        List<string> specimenIds,
        List<string> observationIds)
    {
        var v = FhirGenerationCodes.Observations[seed % FhirGenerationCodes.Observations.Length];
        observationIds.Add(id);
        return Create(id, patientId, encounterId, effective,
                      v.Code, v.Display, v.Category, v.Unit,
                      v.CritLow, v.NormLow, v.NormHigh, v.CritHigh,
                      seed, specimenIds);
    }

    /// <summary>
    /// Create an Observation with caller-supplied values.
    /// Interpretation (H/L/N/HH/LL) is derived automatically from the value vs thresholds.
    /// </summary>
    public static Observation Create(
        string id,
        string patientId,
        string encounterId,
        DateTime effective,
        string loincCode,
        string display,
        string category,
        string unit,
        double critLow,
        double normLow,
        double normHigh,
        double critHigh,
        int seed,
        List<string>? specimenIds = null,
        string? organizationId = null)
    {
        var isLab = category == "laboratory";

        var categoryDisplay = category switch
        {
            "laboratory" => "Laboratory",
            "vital-signs" => "Vital Signs",
            "social-history" => "Social History",
            "survey" => "Survey",
            "imaging" => "Imaging",
            "procedure" => "Procedure",
            _ => category
        };

        var obs = new Observation
        {
            Id = id,
            Status = ObservationStatus.Final,
            Category =
            [
                new CodeableConcept
                {
                    Coding = [new Coding("http://terminology.hl7.org/CodeSystem/observation-category",
                                         category,
                                         categoryDisplay)]
                }
            ],
            Code = Loinc(loincCode, display),
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            Performer = [Ref($"Organization/{organizationId ?? FhirBundleGenerator.HospitalOrgId}", "General Test Hospital")]
        };

        // Use a point-in-time effectiveDateTime for every Observation, including labs.
        // A lab effectivePeriod that starts just before the Daily measurement-period
        // boundary and extends 1-4 hours into the window is included by HAPI date
        // search but can disagree with acquisition-simulator overlap, producing a
        // systematic Observation expected=N actual=N+1 on Daily ACH. Thetis generation
        // already uses FhirDateTime; keep this factory on the same shape.
        obs.Effective = new FhirDateTime(effective);
        if (isLab && specimenIds?.Count > 0)
            obs.Specimen = Ref($"Specimen/{specimenIds[seed % specimenIds.Count]}");

        // ---------------------------------------------------------------
        // Culture / bacteriology — valueString
        // ---------------------------------------------------------------
        if (loincCode == "600-7")
        {
            var organisms = new[] { "No growth", "Staphylococcus aureus", "Escherichia coli", "Klebsiella pneumoniae", "No growth" };
            obs.Value = new FhirString(organisms[seed % organisms.Length]);
            obs.Interpretation = [CC("http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation",
                                     organisms[seed % organisms.Length] == "No growth" ? "N" : "A",
                                     organisms[seed % organisms.Length] == "No growth" ? "Normal" : "Abnormal")];
            return obs;
        }

        // ---------------------------------------------------------------
        // Social history — coded value (e.g. smoking status)
        // ---------------------------------------------------------------
        if (category == "social-history")
        {
            var smokingStatuses = new[]
            {
                ("266919005", "Never smoker"),
                ("8517006",   "Former smoker"),
                ("77176002",  "Smoker"),
                ("266919005", "Never smoker"),
            };
            var (statusCode, statusDisplay) = smokingStatuses[seed % smokingStatuses.Length];
            obs.Value = Snomed(statusCode, statusDisplay);
            return obs;
        }

        // ---------------------------------------------------------------
        // Survey — integer score (e.g. PHQ-9)
        // ---------------------------------------------------------------
        if (category == "survey")
        {
            var score = seed % 28; // PHQ-9 range 0-27
            obs.Value = new Integer(score);
            obs.Interpretation = [InterpretationCode(score, 0, 0, 10, 27)];
            return obs;
        }

        // ---------------------------------------------------------------
        // Blood pressure — component-based
        // ---------------------------------------------------------------
        if (loincCode == "55284-4")
        {
            var systolic = 100 + seed % 80;  // 100–179
            var diastolic = 60 + seed % 40;  // 60–99
            obs.Component =
            [
                new Observation.ComponentComponent
                {
                    Code  = Loinc("8480-6", "Systolic blood pressure"),
                    Value = Qty(systolic, "mm[Hg]"),
                    Interpretation = [InterpretationCode(systolic, 0, 90, 140, 220)]
                },
                new Observation.ComponentComponent
                {
                    Code  = Loinc("8462-4", "Diastolic blood pressure"),
                    Value = Qty(diastolic, "mm[Hg]"),
                    Interpretation = [InterpretationCode(diastolic, 0, 60, 90, 120)]
                }
            ];
            return obs;
        }

        // ---------------------------------------------------------------
        // Standard numeric value — vary within a clinically plausible range
        // using a sine-like distribution so values cluster around normal
        // ---------------------------------------------------------------
        double value;
        if (normHigh > 0 && normHigh < 999)
        {
            // Oscillate around midpoint; ~80% of values will be in-range
            var mid = (normLow + normHigh) / 2.0;
            var range = (normHigh - normLow) * 0.7;
            var phase = seed % 100 / 100.0 * 2 * Math.PI;
            value = Math.Round(mid + Math.Sin(phase) * range, 2);
            value = Math.Max(critLow > 0 ? critLow : 0, Math.Min(critHigh < 999 ? critHigh : value + 1, value));
        }
        else
        {
            // Unbounded high (e.g. height, weight) — just use low + fraction of large range
            value = Math.Round(normLow + seed % 100 / 100.0 * (normLow * 0.5), 1);
        }

        obs.Value = Qty(value, unit);

        // Reference range
        if (normLow > 0 || normHigh < 999)
        {
            obs.ReferenceRange =
            [
                new Observation.ReferenceRangeComponent
                {
                    Low  = normLow > 0    ? Qty(normLow,  unit) : null,
                    High = normHigh < 999 ? Qty(normHigh, unit) : null,
                    Text = normHigh < 999
                        ? $"{normLow} – {normHigh} {unit}"
                        : $"= {normLow} {unit}"
                }
            ];
        }

        // Interpretation flag
        obs.Interpretation = [InterpretationCode(value, critLow, normLow, normHigh, critHigh)];

        return obs;
    }

    // -----------------------------------------------------------------------
    //  Derive an H/L/N/HH/LL interpretation CodeableConcept
    // -----------------------------------------------------------------------
    private static CodeableConcept InterpretationCode(
        double value, double critLow, double normLow, double normHigh, double critHigh)
    {
        const string sys = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation";
        if (critHigh < 999 && value > critHigh) return CC(sys, "HH", "Critical high");
        if (critLow > 0 && value < critLow) return CC(sys, "LL", "Critical low");
        if (normHigh < 999 && value > normHigh) return CC(sys, "H", "High");
        if (normLow > 0 && value < normLow) return CC(sys, "L", "Low");
        return CC(sys, "N", "Normal");
    }
}
