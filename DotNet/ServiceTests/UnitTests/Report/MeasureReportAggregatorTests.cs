using LantanaGroup.Link.Report.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Report;

[Collection("UnitTests")]
public class MeasureReportAggregatorTests
{
    private readonly ILogger<MeasureReportAggregator> _logger;

    public MeasureReportAggregatorTests()
    {
        // Create a mock logger
        var mockLogger = new Mock<ILogger<MeasureReportAggregator>>();
        _logger = mockLogger.Object;
    }

    private MeasureReport GetMeasureReport()
    {

        string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(assemblyLocation, "Resources", "indMeasureReport.json");
        string text = File.ReadAllText(path);
        FhirJsonParser parser = new FhirJsonParser();
        return parser.Parse<MeasureReport>(text);
    }

    [Fact]
    public void TestWithOneMeasureReport()
    {
        List<MeasureReport> measureReports = new List<MeasureReport>();
        MeasureReport measureReport = GetMeasureReport();
        measureReports.Add(measureReport);

        MeasureReportAggregator aggregator = new MeasureReportAggregator(_logger);
        List<MeasureReport> aggregates = aggregator.Aggregate(measureReports, "123", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.Equal(1, aggregates.Count);
        MeasureReport aggregate = aggregates[0];
        Assert.NotNull(aggregate.Measure);
        Assert.Equal(measureReport.Measure, aggregate.Measure);
        Assert.Equal(MeasureReport.MeasureReportType.SubjectList, aggregate.Type);
        Assert.NotNull(aggregate.DateElement);

        // Check that there is a contained resource for the List
        Assert.NotNull(aggregate.Contained);
        Assert.Equal(1, aggregate.Contained.Count);
        Assert.IsType<Hl7.Fhir.Model.List>(aggregate.Contained[0]);
        Hl7.Fhir.Model.List list = (Hl7.Fhir.Model.List)aggregate.Contained[0];
        Assert.NotNull(list.Entry);
        Assert.Equal(1, list.Entry.Count);
        Hl7.Fhir.Model.List.EntryComponent entry = list.Entry[0];
        Assert.NotNull(entry.Item?.Reference);
        Assert.Equal("MeasureReport/" + measureReport.Id, entry.Item.Reference);

        // Check that the group and population is correct
        Assert.NotNull(aggregate.Group);
        Assert.Equal(1, aggregate.Group.Count);
        MeasureReport.GroupComponent group = aggregate.Group[0];
        Assert.NotNull(group.Population);
        MeasureReport.PopulationComponent population = group.Population[0];
        Assert.Equal(3, population.Count);
        Assert.NotNull(population.SubjectResults);
        Assert.Equal("#" + population.Code.Coding[0].Code + "-list", population.SubjectResults.Reference);
        Assert.Equal(population.Code.Coding[0].Code + "-list", list.Id);
    }
}