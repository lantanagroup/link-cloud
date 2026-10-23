using LantanaGroup.Link.PatientFlowSimulator.Planning;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class MonthPlannerTests
{
    private static readonly YearMonth November = new(2026, 11);
    private static readonly TimeZoneInfo Chicago = LocalTime.Resolve("America/Chicago");

    [Fact]
    public void Plan_SameMonthTwice_IsIdentical()
    {
        var scenario = DefaultScenario.Load();

        var first = MonthPlanner.Plan(scenario, November).Stays.Select(Describe).ToList();
        var second = MonthPlanner.Plan(scenario, November).Stays.Select(Describe).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Plan_ConsecutiveMonths_GiveEveryPatientANewIdentity()
    {
        var scenario = DefaultScenario.Load();

        var novemberIds = MonthPlanner.Plan(scenario, November).Stays.Select(s => s.PatientId).ToHashSet();
        var decemberIds = MonthPlanner.Plan(scenario, November.AddMonths(1)).Stays.Select(s => s.PatientId).ToHashSet();

        Assert.Equal(100, novemberIds.Count);
        Assert.Equal(100, decemberIds.Count);
        Assert.Empty(novemberIds.Intersect(decemberIds));
    }

    [Fact]
    public void Plan_DefaultScenario_HasTwentyQualifyingPatientsPerMeasure()
    {
        var scenario = DefaultScenario.Load();

        var stays = MonthPlanner.Plan(scenario, November).Stays;

        foreach (var measure in scenario.Facilities.SelectMany(f => f.Measures).Distinct())
        {
            var qualifying = stays.Count(s => s.Cohort.QualifiesFor.Contains(measure));
            Assert.True(qualifying == 20, $"{measure} has {qualifying} qualifying patients, expected 20.");
        }
    }

    [Fact]
    public void Plan_ForcedCrossMonthStays_StraddleTheMonthBoundary()
    {
        var scenario = DefaultScenario.Load();
        var monthEnd = LocalTime.MonthEndUtc(November, Chicago);

        var forced = MonthPlanner.Plan(scenario, November).Stays.Where(s => s.ForcedCrossMonth).ToList();

        Assert.Equal(7, forced.Count);
        foreach (var stay in forced)
        {
            Assert.True(stay.AdmitUtc < monthEnd, $"{stay.PatientId} was admitted after the month ended.");
            Assert.True(stay.AdmitUtc >= monthEnd.AddHours(-scenario.CrossMonth.AdmitWithinHoursOfMonthEnd));
            Assert.True(stay.DischargeUtc >= monthEnd, $"{stay.PatientId} was discharged before the month ended.");
            Assert.True(stay.DischargeUtc <= monthEnd.AddHours(scenario.CrossMonth.DischargeWithinHoursOfMonthStart + 1));
            Assert.True(stay.CrossesMonthBoundary);
        }
    }

    [Fact]
    public void Plan_ConfinedTemplates_DischargeInsideTheMonth()
    {
        var scenario = DefaultScenario.Load();
        var monthStart = LocalTime.MonthStartUtc(November, Chicago);
        var monthEnd = LocalTime.MonthEndUtc(November, Chicago);

        var confined = MonthPlanner.Plan(scenario, November).Stays
            .Where(s => !s.ForcedCrossMonth && s.Template.ConfineToMonth)
            .ToList();

        Assert.NotEmpty(confined);
        foreach (var stay in confined)
        {
            Assert.True(stay.AdmitUtc >= monthStart);
            Assert.True(stay.DischargeUtc < monthEnd, $"{stay.PatientId} runs past the month end.");
            Assert.False(stay.CrossesMonthBoundary);
        }
    }

    [Fact]
    public void Plan_NextMonth_CarriesOverTheForcedCrossMonthStays()
    {
        var scenario = DefaultScenario.Load();
        var forcedInNovember = MonthPlanner.Plan(scenario, November).Stays
            .Where(s => s.ForcedCrossMonth)
            .Select(s => s.PatientId)
            .ToHashSet();

        var december = MonthPlanner.Plan(scenario, November.AddMonths(1));
        var carriedIds = december.CarryOvers.Select(s => s.PatientId).ToHashSet();

        Assert.True(forcedInNovember.IsSubsetOf(carriedIds));
        Assert.All(december.CarryOvers.Where(s => forcedInNovember.Contains(s.PatientId)),
                   s => Assert.Equal(November, s.PlanMonth));
    }

    [Fact]
    public void Plan_LongTermCareResidents_MostlyCrossTheMonthBoundary()
    {
        var scenario = DefaultScenario.Load();

        var residents = MonthPlanner.Plan(scenario, November).Stays
            .Where(s => s.Cohort.Name == "residents")
            .ToList();

        Assert.Equal(20, residents.Count);
        Assert.True(residents.Count(s => s.CrossesMonthBoundary) >= 10);
    }

    private static string Describe(PatientStay stay)
        => $"{stay.PatientId}|{stay.Identity.DisplayName}|{stay.AdmitUtc:O}|{stay.DischargeUtc:O}";
}
