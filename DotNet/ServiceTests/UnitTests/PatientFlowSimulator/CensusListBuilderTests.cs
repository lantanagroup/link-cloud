using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class CensusListBuilderTests
{
    private static readonly DateTimeOffset Admit = new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Discharge = Admit.AddHours(60);

    private readonly FacilityDefinition _facility = new()
    {
        FacilityId = "unit-facility",
        Name = "Unit Facility",
        PatientIdPrefix = "UF"
    };

    [Fact]
    public void Build_Always_ReturnsTheSixListsLinkExpects()
    {
        var lists = CensusListBuilder.Build(_facility, [], Admit);

        Assert.Equal(6, lists.Count);
        Assert.Equal(CensusListBuilder.ListIds("unit-facility"), lists.Select(l => l.ListId).ToList());
        Assert.All(lists, l => Assert.Empty(l.Entries));
    }

    [Theory]
    [InlineData(1, CensusTimeFrame.LessThan24Hours)]
    [InlineData(30, CensusTimeFrame.Between24To48Hours)]
    [InlineData(55, CensusTimeFrame.MoreThan48Hours)]
    public void Build_AdmittedPatient_IsOnExactlyOneAdmitList(int hoursAfterAdmit, CensusTimeFrame expected)
    {
        var stay = Stay();

        var lists = CensusListBuilder.Build(_facility, [stay], Admit.AddHours(hoursAfterAdmit));

        var containing = lists.Where(l => l.Entries.Any(e => e.PatientId == stay.PatientId)).ToList();
        var only = Assert.Single(containing);
        Assert.Equal(CensusListStatus.Admit, only.Status);
        Assert.Equal(expected, only.TimeFrame);
    }

    [Theory]
    [InlineData(1, CensusTimeFrame.LessThan24Hours)]
    [InlineData(36, CensusTimeFrame.Between24To48Hours)]
    [InlineData(71, CensusTimeFrame.MoreThan48Hours)]
    public void Build_DischargedPatient_MovesToTheDischargeLists(int hoursAfterDischarge, CensusTimeFrame expected)
    {
        var stay = Stay();

        var lists = CensusListBuilder.Build(_facility, [stay], Discharge.AddHours(hoursAfterDischarge));

        var only = Assert.Single(lists, l => l.Entries.Any(e => e.PatientId == stay.PatientId));
        Assert.Equal(CensusListStatus.Discharge, only.Status);
        Assert.Equal(expected, only.TimeFrame);
    }

    [Fact]
    public void Build_PatientDischargedOverThreeDaysAgo_IsOnNoList()
    {
        var stay = Stay();

        var lists = CensusListBuilder.Build(_facility, [stay], Discharge.AddHours(73));

        Assert.All(lists, l => Assert.DoesNotContain(l.Entries, e => e.PatientId == stay.PatientId));
    }

    [Fact]
    public void Build_PatientNotYetAdmitted_IsOnNoList()
    {
        var stay = Stay();

        var lists = CensusListBuilder.Build(_facility, [stay], Admit.AddMinutes(-1));

        Assert.All(lists, l => Assert.Empty(l.Entries));
    }

    private PatientStay Stay()
    {
        var cohort = new CohortDefinition { Name = "unit", Template = "unit", Count = 1 };
        var template = new ClinicalTemplate();
        return new PatientStay
        {
            Facility = _facility,
            Cohort = cohort,
            Template = template,
            PlanMonth = new YearMonth(2026, 11),
            Index = 0,
            Seed = "unit",
            Identity = new PatientIdentity("UF1234567890", "Ada", "Lovelace", "female", new DateOnly(1960, 1, 1)),
            AdmitUtc = Admit,
            DischargeUtc = Discharge,
            ForcedCrossMonth = false,
            CrossesMonthBoundary = false
        };
    }
}
