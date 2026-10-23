using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// Turns a month plan into hourly transaction bundles. The first hour of the month also carries the
/// facility's shared resources and the patients carried over from earlier months, so a month can be
/// imported on its own.
/// </summary>
public sealed class HourlyBundleBuilder
{
    private readonly ScenarioDefinition _scenario;

    public HourlyBundleBuilder(ScenarioDefinition scenario)
    {
        _scenario = scenario;
    }

    /// <summary>
    /// Generates the month.
    /// </summary>
    public MonthOutput Build(YearMonth month)
    {
        var plan = MonthPlanner.Plan(_scenario, month);
        var hours = new SortedDictionary<DateTimeOffset, HourlyEntries>();
        var manifest = new MonthManifest
        {
            Month = month.ToString(),
            Scenario = _scenario.Name,
            Seed = _scenario.Seed,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            LookbackMonths = MonthPlanner.LookbackMonths(_scenario)
        };

        foreach (var facility in _scenario.Facilities)
        {
            var zone = LocalTime.Resolve(facility.TimeZone);
            var startUtc = LocalTime.MonthStartUtc(month, zone);
            var endUtc = LocalTime.MonthEndUtc(month, zone);
            var stays = plan.StaysFor(facility);

            AddSharedResources(hours, facility, stays, startUtc);
            AddCarryOverBaselines(hours, plan, facility, startUtc);
            AddStayEvents(hours, stays, startUtc, endUtc);
            AddCensusLists(hours, facility, stays, startUtc, endUtc);

            manifest.Facilities.Add(BuildFacilityManifest(facility, zone, plan, startUtc, endUtc));
        }

        var files = hours
            .Where(pair => pair.Value.Count > 0)
            .Select(pair => new HourlyFile(pair.Key, pair.Value.ToBundle(pair.Key)))
            .ToList();

        manifest.FileCount = files.Count;
        manifest.EntryCount = files.Sum(f => f.EntryCount);
        manifest.FirstFile = files.FirstOrDefault()?.FileName;
        manifest.LastFile = files.LastOrDefault()?.FileName;

        return new MonthOutput(plan, files, manifest);
    }

    private static HourlyEntries EntriesFor(SortedDictionary<DateTimeOffset, HourlyEntries> hours, DateTimeOffset instant)
    {
        var hour = LocalTime.FloorToHour(instant);
        if (!hours.TryGetValue(hour, out var entries))
        {
            entries = new HourlyEntries();
            hours[hour] = entries;
        }

        return entries;
    }

    private static void AddSharedResources(SortedDictionary<DateTimeOffset, HourlyEntries> hours,
                                           FacilityDefinition facility,
                                           IReadOnlyList<PatientStay> stays,
                                           DateTimeOffset startUtc)
    {
        var firstHour = EntriesFor(hours, startUtc);
        firstHour.Add(FhirResourceFactory.BuildOrganization(facility));

        var locations = stays
            .Select(s => s.Template.Location)
            .Where(l => l != null)
            .DistinctBy(l => l!.Hsloc);
        foreach (var location in locations)
        {
            firstHour.Add(FhirResourceFactory.BuildLocation(facility, location!));
        }
    }

    // A carried-over patient is re-sent in the state they are in when the month starts, so the month's
    // files work even when the earlier month was never imported. Every entry is a PUT, so this is
    // harmless when it was.
    private static void AddCarryOverBaselines(SortedDictionary<DateTimeOffset, HourlyEntries> hours,
                                              MonthPlan plan,
                                              FacilityDefinition facility,
                                              DateTimeOffset startUtc)
    {
        var firstHour = EntriesFor(hours, startUtc);
        foreach (var stay in plan.CarryOvers.Where(s => SameFacility(s, facility)))
        {
            var finished = stay.DischargeUtc <= startUtc;
            AddAdmissionResources(firstHour, stay, finished);
        }
    }

    private static void AddStayEvents(SortedDictionary<DateTimeOffset, HourlyEntries> hours,
                                      IReadOnlyList<PatientStay> stays,
                                      DateTimeOffset startUtc,
                                      DateTimeOffset endUtc)
    {
        foreach (var stay in stays)
        {
            foreach (var stayEvent in ClinicalEventPlanner.EventsFor(stay))
            {
                if (stayEvent.WhenUtc < startUtc || stayEvent.WhenUtc >= endUtc)
                {
                    continue;
                }

                AddEvent(EntriesFor(hours, stayEvent.WhenUtc), stayEvent);
            }
        }
    }

    private static void AddEvent(HourlyEntries entries, StayEvent stayEvent)
    {
        switch (stayEvent.Kind)
        {
            case StayEventKind.Admit:
                AddAdmissionResources(entries, stayEvent.Stay, finished: false);
                break;
            case StayEventKind.Observation:
                entries.Add(FhirResourceFactory.BuildObservation(stayEvent));
                break;
            case StayEventKind.MedicationAdministration:
                entries.Add(FhirResourceFactory.BuildMedicationAdministration(stayEvent));
                break;
            case StayEventKind.Discharge:
                entries.Add(FhirResourceFactory.BuildEncounter(stayEvent.Stay, finished: true));
                break;
        }
    }

    private static void AddAdmissionResources(HourlyEntries entries, PatientStay stay, bool finished)
    {
        entries.Add(FhirResourceFactory.BuildPatient(stay));
        entries.Add(FhirResourceFactory.BuildEncounter(stay, finished));

        foreach (var condition in FhirResourceFactory.BuildConditions(stay))
        {
            entries.Add(condition);
        }

        foreach (var request in FhirResourceFactory.BuildMedicationRequests(stay))
        {
            entries.Add(request);
        }
    }

    // Lists are written in every hour in which any of them changed, and all six in the first hour so they
    // exist before Link first reads them. Membership is taken at the last instant of the hour: a patient
    // admitted exactly on the next hour belongs to the next file, together with their Patient resource.
    private static void AddCensusLists(SortedDictionary<DateTimeOffset, HourlyEntries> hours,
                                       FacilityDefinition facility,
                                       IReadOnlyList<PatientStay> stays,
                                       DateTimeOffset startUtc,
                                       DateTimeOffset endUtc)
    {
        IReadOnlyList<CensusListSnapshot>? previous = null;

        for (var hour = LocalTime.FloorToHour(startUtc); hour < endUtc; hour = hour.AddHours(1))
        {
            var hourEnd = hour.AddHours(1);
            var snapshots = CensusListBuilder.Build(facility, stays, hourEnd.AddTicks(-1));

            for (var i = 0; i < snapshots.Count; i++)
            {
                if (previous != null && snapshots[i].SameEntriesAs(previous[i]))
                {
                    continue;
                }

                EntriesFor(hours, hour).Add(FhirResourceFactory.BuildCensusList(snapshots[i], facility, hourEnd));
            }

            previous = snapshots;
        }
    }

    private static FacilityManifest BuildFacilityManifest(FacilityDefinition facility,
                                                          TimeZoneInfo zone,
                                                          MonthPlan plan,
                                                          DateTimeOffset startUtc,
                                                          DateTimeOffset endUtc)
    {
        var own = plan.Stays.Where(s => SameFacility(s, facility)).ToList();
        var carried = plan.CarryOvers.Where(s => SameFacility(s, facility)).ToList();
        var overlappingCarryOvers = carried.Where(s => s.DischargeUtc > startUtc).ToList();

        var result = new FacilityManifest
        {
            FacilityId = facility.FacilityId,
            Name = facility.Name,
            TimeZone = facility.TimeZone,
            MonthStartUtc = startUtc.ToString("O"),
            MonthEndUtc = endUtc.ToString("O"),
            Measures = facility.Measures.ToList(),
            CensusListIds = CensusListBuilder.ListIds(facility.FacilityId).ToList(),
            PatientsAdmittedThisMonth = own.Count,
            PatientsCarriedOver = carried.Count,
            Patients = own.Select(s => ToPatientManifest(s, zone)).ToList(),
            CarryOvers = carried.Select(s => ToPatientManifest(s, zone)).ToList()
        };

        foreach (var measure in facility.Measures)
        {
            result.ExpectedQualifying[measure] = own.Count(s => Qualifies(s, measure));
            result.ExpectedQualifyingWithCarryOvers[measure] =
                own.Count(s => Qualifies(s, measure)) + overlappingCarryOvers.Count(s => Qualifies(s, measure));
        }

        return result;
    }

    private static bool Qualifies(PatientStay stay, string measure)
        => stay.Cohort.QualifiesFor.Any(m => string.Equals(m, measure, StringComparison.OrdinalIgnoreCase));

    private static bool SameFacility(PatientStay stay, FacilityDefinition facility)
        => string.Equals(stay.Facility.FacilityId, facility.FacilityId, StringComparison.OrdinalIgnoreCase);

    private static PatientManifest ToPatientManifest(PatientStay stay, TimeZoneInfo zone) => new()
    {
        PatientId = stay.PatientId,
        Name = stay.Identity.DisplayName,
        Gender = stay.Identity.Gender,
        BirthDate = stay.Identity.BirthDate.ToString("yyyy-MM-dd"),
        Cohort = stay.Cohort.Name,
        Template = stay.Cohort.Template,
        PlannedInMonth = stay.PlanMonth.ToString(),
        AdmitUtc = stay.AdmitUtc,
        DischargeUtc = stay.DischargeUtc,
        AdmitLocal = LocalTime.ToLocal(stay.AdmitUtc, zone).ToString("yyyy-MM-dd HH:mm"),
        DischargeLocal = LocalTime.ToLocal(stay.DischargeUtc, zone).ToString("yyyy-MM-dd HH:mm"),
        LengthOfStayHours = Math.Round((stay.DischargeUtc - stay.AdmitUtc).TotalHours, 1),
        CrossesMonthBoundary = stay.CrossesMonthBoundary,
        ForcedCrossMonth = stay.ForcedCrossMonth,
        QualifiesFor = stay.Cohort.QualifiesFor.ToList()
    };
}
