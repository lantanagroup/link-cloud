using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Turns a scenario and a month into concrete stays. Pure and deterministic: planning the same month
/// twice gives the same patients, so a later month can recompute the earlier months it carries over.
/// </summary>
public static class MonthPlanner
{
    /// <summary>
    /// Plans a month, including the stays carried over from earlier months.
    /// </summary>
    public static MonthPlan Plan(ScenarioDefinition scenario, YearMonth month)
    {
        var stays = PlanStaysAdmittedIn(scenario, month);
        var carryOvers = new List<PatientStay>();
        var lookback = LookbackMonths(scenario);
        var zones = scenario.Facilities.ToDictionary(f => f.FacilityId, f => LocalTime.Resolve(f.TimeZone));

        for (var back = 1; back <= lookback; back++)
        {
            foreach (var stay in PlanStaysAdmittedIn(scenario, month.AddMonths(-back)))
            {
                var monthStartUtc = LocalTime.MonthStartUtc(month, zones[stay.Facility.FacilityId]);
                if (stay.ReachesInto(monthStartUtc, CensusListBuilder.TrailingWindow))
                {
                    carryOvers.Add(stay);
                }
            }
        }

        return new MonthPlan(month, stays, carryOvers);
    }

    /// <summary>
    /// Plans only the stays admitted in the month.
    /// </summary>
    public static IReadOnlyList<PatientStay> PlanStaysAdmittedIn(ScenarioDefinition scenario, YearMonth month)
    {
        var result = new List<PatientStay>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var facility in scenario.Facilities)
        {
            var zone = LocalTime.Resolve(facility.TimeZone);
            foreach (var cohort in facility.Cohorts)
            {
                var template = scenario.GetTemplate(cohort.Template);
                for (var index = 0; index < cohort.Count; index++)
                {
                    result.Add(PlanStay(scenario, facility, cohort, template, zone, month, index, usedIds));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// How many earlier months can still have patients in the facility when a month starts. Derived from
    /// the longest template stay plus the discharge-list window.
    /// </summary>
    public static int LookbackMonths(ScenarioDefinition scenario)
    {
        var longestStayHours = scenario.Templates.Values
            .Select(t => t.LengthOfStayHours.Max)
            .DefaultIfEmpty(0)
            .Max();
        var reachHours = longestStayHours
                         + CensusListBuilder.TrailingWindow.TotalHours
                         + scenario.CrossMonth.DischargeWithinHoursOfMonthStart;

        return (int)Math.Ceiling(reachHours / (28 * 24.0)) + 1;
    }

    private static PatientStay PlanStay(ScenarioDefinition scenario,
                                        FacilityDefinition facility,
                                        CohortDefinition cohort,
                                        ClinicalTemplate template,
                                        TimeZoneInfo zone,
                                        YearMonth month,
                                        int index,
                                        HashSet<string> usedIds)
    {
        var seed = $"{scenario.Seed}|{facility.FacilityId}|{cohort.Name}|{month}|{index}";
        var random = DeterministicRandom.Create(seed);
        var identity = BuildIdentity(random, facility, template, month, usedIds);
        var lengthOfStayHours = random.NextInt(template.LengthOfStayHours);
        var forcedCross = index < cohort.CrossMonthCount;
        var (admitLocal, dischargeLocal) = forcedCross
            ? PlaceCrossMonthStay(random, month, lengthOfStayHours, scenario.CrossMonth)
            : PlaceStay(random, month, lengthOfStayHours, template.ConfineToMonth);

        return new PatientStay
        {
            Facility = facility,
            Cohort = cohort,
            Template = template,
            PlanMonth = month,
            Index = index,
            Seed = seed,
            Identity = identity,
            AdmitUtc = LocalTime.ToUtc(admitLocal, zone),
            DischargeUtc = LocalTime.ToUtc(dischargeLocal, zone),
            ForcedCrossMonth = forcedCross,
            CrossesMonthBoundary = YearMonth.Of(dischargeLocal) != month
        };
    }

    private static PatientIdentity BuildIdentity(Random random,
                                                 FacilityDefinition facility,
                                                 ClinicalTemplate template,
                                                 YearMonth month,
                                                 HashSet<string> usedIds)
    {
        var gender = random.Next(2) == 0 ? "female" : "male";
        var givenName = random.Pick(gender == "female" ? NamePools.FemaleGivenNames : NamePools.MaleGivenNames);
        var familyName = random.Pick(NamePools.FamilyNames);
        var ageYears = random.NextInt(template.AgeRange);
        var birthDate = DateOnly.FromDateTime(month.LocalStart)
            .AddYears(-ageYears)
            .AddDays(-random.Next(0, 365));

        string patientId;
        do
        {
            patientId = $"{facility.PatientIdPrefix}{random.NextInt64(1_000_000_000L, 10_000_000_000L)}";
        } while (!usedIds.Add(patientId));

        return new PatientIdentity(patientId, givenName, familyName, gender, birthDate);
    }

    // Admitted in the last hours of the month, discharged in the first hours of the next one.
    private static (DateTime Admit, DateTime Discharge) PlaceCrossMonthStay(Random random,
                                                                             YearMonth month,
                                                                             int lengthOfStayHours,
                                                                             CrossMonthSettings settings)
    {
        var monthEnd = month.LocalEndExclusive;
        var maxHoursBeforeEnd = Math.Max(1, Math.Min(lengthOfStayHours - 1, settings.AdmitWithinHoursOfMonthEnd));
        var hoursBeforeEnd = random.Next(1, maxHoursBeforeEnd + 1);
        var admit = monthEnd.AddHours(-hoursBeforeEnd).AddMinutes(random.Next(0, 60));
        var discharge = admit.AddHours(lengthOfStayHours);
        var latestDischarge = monthEnd.AddHours(settings.DischargeWithinHoursOfMonthStart);

        if (discharge > latestDischarge)
        {
            discharge = monthEnd
                .AddHours(random.Next(1, settings.DischargeWithinHoursOfMonthStart + 1))
                .AddMinutes(random.Next(0, 60));
        }

        return (admit, discharge);
    }

    // Admitted anywhere in the month. Pulled earlier when the stay would otherwise run past month end.
    private static (DateTime Admit, DateTime Discharge) PlaceStay(Random random,
                                                                   YearMonth month,
                                                                   int lengthOfStayHours,
                                                                   bool confineToMonth)
    {
        var monthStart = month.LocalStart;
        var monthEnd = month.LocalEndExclusive;
        var admit = monthStart
            .AddDays(random.Next(0, month.Days))
            .AddHours(random.NextWeightedHourOfDay())
            .AddMinutes(random.Next(0, 60));
        var discharge = admit.AddHours(lengthOfStayHours);

        if (confineToMonth && discharge >= monthEnd)
        {
            admit = monthEnd.AddHours(-lengthOfStayHours - random.Next(1, 25)).AddMinutes(random.Next(0, 60));
            if (admit < monthStart)
            {
                admit = monthStart.AddHours(random.Next(0, 7)).AddMinutes(random.Next(0, 60));
            }

            discharge = admit.AddHours(lengthOfStayHours);
        }

        return (admit, discharge);
    }
}
