using System.Reflection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using List = NUnit.Framework.List;

namespace LantanaGroup.Link.ReportTests.Core;

public class MeasureReportAggregatorTests
{
    private static ILogger<MeasureReportAggregator> _logger;
    
    [SetUp]
    public void Setup()
    {
        // Create a mock logger
        var mockLogger = new Mock<ILogger<MeasureReportAggregator>>();
        _logger = mockLogger.Object;
    }
    
    private MeasureReport getMeasureReport()
    {
        string resourcePath = "LantanaGroup.Link.ReportTests.Samples.indMeasureReport.json";

        // Get the executing assembly
        Assembly assembly = Assembly.GetExecutingAssembly();

        // Read the embedded resource as a stream
        using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null)
            {
                throw new Exception($"Resource {resourcePath} not found");
            }
            
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                FhirJsonParser parser = new FhirJsonParser();
                MeasureReport measureReport = parser.Parse<MeasureReport>(json);
                return measureReport;
            }
        }
    }
    
    [Test]
    public void TestWithOneMeasureReport()
    {
        List<MeasureReport> measureReports = new List<MeasureReport>();
        MeasureReport measureReport = getMeasureReport();
        measureReports.Add(measureReport);
        
        MeasureReportAggregator aggregator = new MeasureReportAggregator(_logger);
        List<MeasureReport> aggregates = aggregator.Aggregate(measureReports, "123", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        
        Assert.That(aggregates.Count, Is.EqualTo(1));
        MeasureReport aggregate = aggregates[0];
        Assert.IsNotNull(aggregate.Measure);
        Assert.That(aggregate.Measure, Is.EqualTo(measureReport.Measure));
        Assert.That(aggregate.Type, Is.EqualTo(MeasureReport.MeasureReportType.SubjectList));
        Assert.That(aggregate.DateElement, Is.Not.Null);
        
        // Check that there is a contained resource for the List
        Assert.That(aggregate.Contained, Is.Not.Null);
        Assert.That(aggregate.Contained.Count, Is.EqualTo(1));
        Assert.IsInstanceOf(typeof(Hl7.Fhir.Model.List), aggregate.Contained[0]);
        Hl7.Fhir.Model.List list = (Hl7.Fhir.Model.List)aggregate.Contained[0];
        Assert.That(list.Entry, Is.Not.Null);
        Assert.That(list.Entry.Count, Is.EqualTo(1));
        Hl7.Fhir.Model.List.EntryComponent entry = list.Entry[0];
        Assert.That(entry.Item?.Reference, Is.Not.Null);
        Assert.That(entry.Item.Reference, Is.EqualTo("MeasureReport/" + measureReport.Id));

        // Check that the group and population is correct
        Assert.IsNotNull(aggregate.Group);
        Assert.That(aggregate.Group.Count, Is.EqualTo(1));
        MeasureReport.GroupComponent group = aggregate.Group[0];
        Assert.That(group.Population, Is.Not.Null);
        MeasureReport.PopulationComponent population = group.Population[0];
        Assert.That(population.Count, Is.EqualTo(3));
        Assert.That(population.SubjectResults, Is.Not.Null);
        Assert.That(population.SubjectResults.Reference, Is.EqualTo("#" + population.Code.Coding[0].Code + "-list"));
        Assert.That(list.Id, Is.EqualTo(population.Code.Coding[0].Code + "-list"));
    }
}