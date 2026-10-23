using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Planning;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class HourlyBundleBuilderTests
{
    private static readonly YearMonth November = new(2026, 11);

    [Fact]
    public void Build_FirstFileOfTheMonth_CarriesEveryCensusListAndSharedResource()
    {
        var scenario = DefaultScenario.Load();

        var output = new HourlyBundleBuilder(scenario).Build(November);
        var first = output.Files[0].Bundle;

        var listIds = first.Entry.Select(e => e.Resource).OfType<List>().Select(l => l.Id).ToList();
        Assert.Equal(18, listIds.Count);
        foreach (var facility in scenario.Facilities)
        {
            Assert.All(CensusListBuilder.ListIds(facility.FacilityId), id => Assert.Contains(id, listIds));
        }

        Assert.Equal(3, first.Entry.Select(e => e.Resource).OfType<Organization>().Count());
    }

    [Fact]
    public void Build_EveryFile_IsATransactionOfPutsWithUrlsMatchingTheResources()
    {
        var output = new HourlyBundleBuilder(DefaultScenario.Load()).Build(November);

        Assert.NotEmpty(output.Files);
        foreach (var file in output.Files)
        {
            Assert.Equal(Bundle.BundleType.Transaction, file.Bundle.Type);
            Assert.NotEmpty(file.Bundle.Entry);
            foreach (var entry in file.Bundle.Entry)
            {
                Assert.NotNull(entry.Request);
                Assert.NotNull(entry.Resource);
                Assert.Equal(Bundle.HTTPVerb.PUT, entry.Request.Method);
                Assert.Equal($"{entry.Resource.TypeName}/{entry.Resource.Id}", entry.Request.Url);
            }
        }
    }

    [Fact]
    public void Build_EveryFile_HasNoResourceTwice()
    {
        var output = new HourlyBundleBuilder(DefaultScenario.Load()).Build(November);

        foreach (var file in output.Files)
        {
            var urls = file.Bundle.Entry.Select(e => e.Request?.Url).ToList();
            Assert.Equal(urls.Count, urls.Distinct().Count());
        }
    }

    [Fact]
    public void Build_Files_StayInsideTheFacilityMonth()
    {
        var scenario = DefaultScenario.Load();
        var zone = LocalTime.Resolve(scenario.Facilities[0].TimeZone);

        var output = new HourlyBundleBuilder(scenario).Build(November);

        Assert.Equal(LocalTime.MonthStartUtc(November, zone), output.Files[0].Hour);
        Assert.True(output.Files[^1].Hour < LocalTime.MonthEndUtc(November, zone));
    }

    [Fact]
    public void Build_CrossMonthStay_IsDischargedInTheNextMonthsFiles()
    {
        var scenario = DefaultScenario.Load();
        var builder = new HourlyBundleBuilder(scenario);
        var forced = MonthPlanner.Plan(scenario, November).Stays.First(s => s.ForcedCrossMonth);

        var november = builder.Build(November);
        var december = builder.Build(November.AddMonths(1));

        var admitted = november.Files.SelectMany(f => f.Bundle.Entry).Select(e => e.Resource).OfType<Encounter>()
            .Where(e => e.Id == forced.EncounterId).ToList();
        Assert.All(admitted, e => Assert.Equal(Encounter.EncounterStatus.InProgress, e.Status));

        var finished = december.Files.SelectMany(f => f.Bundle.Entry).Select(e => e.Resource).OfType<Encounter>()
            .Where(e => e.Id == forced.EncounterId && e.Status == Encounter.EncounterStatus.Finished).ToList();
        var discharge = Assert.Single(finished);
        Assert.NotNull(discharge.Period?.EndElement);
        Assert.Equal(forced.DischargeUtc, discharge.Period.EndElement.ToDateTimeOffset(TimeSpan.Zero));
    }

    [Fact]
    public void Build_Manifest_ReportsTwentyNewQualifyingPatientsPerMeasure()
    {
        var output = new HourlyBundleBuilder(DefaultScenario.Load()).Build(November);

        foreach (var facility in output.Manifest.Facilities)
        {
            foreach (var measure in facility.Measures)
            {
                Assert.Equal(20, facility.ExpectedQualifying[measure]);
                Assert.True(facility.ExpectedQualifyingWithCarryOvers[measure] >= 20);
            }
        }
    }
}
