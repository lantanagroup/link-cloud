using System.Text.RegularExpressions;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Reporting;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class PatientFlowReportBuilderTests
{
    private static readonly YearMonth November = new(2026, 11);

    [Fact]
    public void Build_DefaultNovember_HasASectionAndAChartForEveryPatient()
    {
        var scenario = DefaultScenario.Load();
        var plan = MonthPlanner.Plan(scenario, November);

        var report = new PatientFlowReportBuilder(scenario).Build(November);

        foreach (var stay in plan.Stays.Concat(plan.CarryOvers))
        {
            Assert.Contains($"#### {stay.PatientId}, {stay.Identity.DisplayName}", report);
            Assert.Contains($"title {stay.Identity.DisplayName} ({stay.PatientId})", report);
        }

        // The report uses the platform newline, so allow the carriage return before the end of line.
        var charts = Regex.Matches(report, "^```mermaid\r?$", RegexOptions.Multiline).Count;
        Assert.Equal(1 + scenario.Facilities.Count + plan.Stays.Count + plan.CarryOvers.Count, charts);
    }

    [Fact]
    public void Build_GanttTaskLines_HaveNoColonOrCommaInTheNameAndTwoDatesAfterIt()
    {
        var report = new PatientFlowReportBuilder(DefaultScenario.Load()).Build(November);
        var taskLines = new List<string>();
        var inGantt = false;
        foreach (var raw in report.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line == "gantt")
            {
                inGantt = true;
            }
            else if (line == "```")
            {
                inGantt = false;
            }
            else if (inGantt && line.Contains(" :", StringComparison.Ordinal))
            {
                taskLines.Add(line);
            }
        }

        Assert.NotEmpty(taskLines);
        foreach (var line in taskLines)
        {
            var name = line[..line.IndexOf(" :", StringComparison.Ordinal)];
            Assert.DoesNotContain(':', name);
            Assert.DoesNotContain(',', name);
            Assert.Matches(@" :(crit, |done, |active, )?\d{4}-\d{2}-\d{2} \d{2}:\d{2}, \d{4}-\d{2}-\d{2} \d{2}:\d{2}$", line);
        }
    }

    [Fact]
    public void CensusPhase_ShortVisit_SkipsTheAdmitPhasesItNeverReaches()
    {
        var scenario = DefaultScenario.Load();
        var visit = MonthPlanner.Plan(scenario, November).Stays.First(s => s.Cohort.Name == "clinic-visits");

        var phases = CensusPhase.For(visit);

        Assert.Equal(["Admit list under 24h", "Discharge list under 24h", "Discharge list 24 to 48h", "Discharge list over 48h"],
                     phases.Select(p => p.Label).ToList());
        Assert.Equal(visit.DischargeUtc, phases[0].EndUtc);
        Assert.Equal(visit.DischargeUtc.AddHours(72), phases[^1].EndUtc);
    }
}
