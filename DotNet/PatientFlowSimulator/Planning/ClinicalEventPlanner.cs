namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Expands a stay into the timed events that produce its FHIR resources.
/// </summary>
public static class ClinicalEventPlanner
{
    /// <summary>
    /// Safety cap on repeats of one observation or medication inside one stay.
    /// </summary>
    public const int MaxRepeatsPerSpec = 400;

    /// <summary>
    /// Every event of the stay, in time order.
    /// </summary>
    public static IReadOnlyList<StayEvent> EventsFor(PatientStay stay)
    {
        var events = new List<StayEvent>
        {
            new(stay.AdmitUtc, StayEventKind.Admit, stay, 0, null, null, 0)
        };

        foreach (var spec in stay.Template.Observations)
        {
            var random = DeterministicRandom.Create(stay.Seed, "observation", spec.Loinc);
            var when = stay.AdmitUtc.AddHours(1);
            for (var n = 1; when < stay.DischargeUtc && n <= MaxRepeatsPerSpec; n++)
            {
                var value = Math.Round(random.NextDouble(spec.Min, spec.Max), spec.Decimals);
                events.Add(new StayEvent(when, StayEventKind.Observation, stay, n, spec, null, value));
                when = when.AddHours(spec.EveryHours);
            }
        }

        foreach (var spec in stay.Template.Medications)
        {
            var when = stay.AdmitUtc.AddHours(2);
            for (var n = 1; when < stay.DischargeUtc && n <= MaxRepeatsPerSpec; n++)
            {
                events.Add(new StayEvent(when, StayEventKind.MedicationAdministration, stay, n, null, spec, spec.DoseValue));
                when = when.AddHours(spec.EveryHours);
            }
        }

        events.Add(new StayEvent(stay.DischargeUtc, StayEventKind.Discharge, stay, 0, null, null, 0));

        return events
            .OrderBy(e => e.WhenUtc)
            .ThenBy(e => e.Kind)
            .ToList();
    }
}
