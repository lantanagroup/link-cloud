using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Collections.Immutable;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.Core;

/// <summary>
/// This Class Generates the Aggregate bundle based on the provided individual MeasureReports.
/// These Aggregate Bundles are part of the overall submission step.
/// </summary>
public class MeasureReportAggregator
{
    private const string AggregateMeasureReportProfile =
        "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport";

    private readonly ILogger<MeasureReportAggregator> _logger;

    private readonly IReportPopulationManager _reportPopulationManager;

    public MeasureReportAggregator(ILogger<MeasureReportAggregator> logger, IReportPopulationManager reportPopulationManager)
    {
        _logger = logger;
        _reportPopulationManager = reportPopulationManager;
    }

    public async Task<List<MeasureReport>> CreateMeasureReportAggregate(ReportScheduleModel reportSchedule, string organizationId)
    {
        var parser = new FhirJsonParser();

        var reportPopulations = await _reportPopulationManager.FindAsync(rp => rp.ReportScheduleId == reportSchedule.Id);

        List<MeasureReport> aggregates = new List<MeasureReport>();

        foreach (var reportPopulation in reportPopulations)
        {
            MeasureReport measureReport = new MeasureReport();

            measureReport.Meta = new Meta()
            {
                Profile = ImmutableList.Create(AggregateMeasureReportProfile)
            };
            measureReport.Id = Guid.NewGuid().ToString();
            measureReport.Type = MeasureReport.MeasureReportType.SubjectList;
            measureReport.Status = MeasureReport.MeasureReportStatus.Complete;
            measureReport.DateElement = FhirDateTime.Now();
            measureReport.Measure = reportPopulation.Measure;
            measureReport.Period = new Period(new FhirDateTime(new DateTimeOffset(reportSchedule.ReportStartDate)), new FhirDateTime(new DateTimeOffset(reportSchedule.ReportEndDate)));
            measureReport.Reporter = new ResourceReference($"Organization/{organizationId}");

            foreach (var groupPopulation in reportPopulation.GroupPopulations)
            {
                List measureReportList = new List();

                foreach (var measureReportPopulation in groupPopulation.MeasureReportPopulations)
                {
                    measureReportList.Entry.Add(new List.EntryComponent()
                    {
                        Item = new ResourceReference()
                        {
                            Reference = "MeasureReport/" + measureReportPopulation.MeasureReportId
                        }
                    });
                }

                measureReport.Contained.Add(measureReportList);
                measureReport.Group.Add(new MeasureReport.GroupComponent()
                {
                    Population = new List<MeasureReport.PopulationComponent>() {
                        new MeasureReport.PopulationComponent() {
                            Code = JsonSerializer.Deserialize<CodeableConcept>(groupPopulation.PopulationCodeJson, LinkFhirSerializerOptions.ForFhirLenientSerialization),
                            Count = groupPopulation.TotalPopulationCount,
                            SubjectResults = new ResourceReference("#" + groupPopulation.PopulationId + "-list")
                        }
                    }
                });
            }

            aggregates.Add(measureReport);
        }

        return aggregates;
    }
}