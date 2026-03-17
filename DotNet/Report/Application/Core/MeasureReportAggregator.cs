using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
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
    private readonly IDatabase _database;

    public MeasureReportAggregator(ILogger<MeasureReportAggregator> logger, IDatabase database)
    {
        _logger = logger;
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<List<MeasureReport>> CreateMeasureReportAggregate(ReportScheduleModel reportSchedule, string organizationId)
    {
        var parser = new FhirJsonParser();
        var populationModels = await _database.ReportPopulationRepository.FindAsync(x => x.ReportScheduleId == reportSchedule.Id);

        List<MeasureReport> aggregates = new List<MeasureReport>();

        foreach (var populationModel in populationModels)
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
            measureReport.Measure = populationModel.Measure;
            measureReport.Period = new Period(new FhirDateTime(new DateTimeOffset(reportSchedule.ReportStartDate)), new FhirDateTime(new DateTimeOffset(reportSchedule.ReportEndDate)));
            measureReport.Reporter = new ResourceReference($"Organization/{organizationId}");

            foreach (var reportPopulation in populationModel.GroupPopulations)
            {
                List measureReportList = new List();

                foreach (var measureReportPopulation in reportPopulation.MeasureReportPopulations)
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
                            Code = JsonSerializer.Deserialize<CodeableConcept>(reportPopulation.PopulationCodeJson, LinkFhirSerializerOptions.ForFhirLenientSerialization),
                            Count = reportPopulation.TotalPopulationCount,
                            SubjectResults = new ResourceReference("#" + reportPopulation.PopulationId + "-list")
                        }
                    }
                });
            }

            aggregates.Add(measureReport);
        }

        return aggregates;
    }
}