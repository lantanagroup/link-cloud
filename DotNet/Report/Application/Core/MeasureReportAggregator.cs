using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Collections.Immutable;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.Core;

/// <summary>
/// Generates the final Aggregate MeasureReport bundles (subject-list type) for submission.
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
        var reportPopulations = await _reportPopulationManager.FindAsync(rp => rp.ReportScheduleId == reportSchedule.Id);

        var aggregates = new List<MeasureReport>();

        foreach (var reportPopulation in reportPopulations)
        {
            var measureReport = new MeasureReport
            {
                Meta = new Meta { Profile = ImmutableList.Create(AggregateMeasureReportProfile) },
                Id = Guid.NewGuid().ToString(),
                Type = MeasureReport.MeasureReportType.SubjectList,
                MeasureElement = new Canonical(reportPopulation.Measure),
                Status = MeasureReport.MeasureReportStatus.Complete,
                DateElement = FhirDateTime.Now(),
                Measure = reportPopulation.Measure,
                Period = new Period(
                    new FhirDateTime(new DateTimeOffset(reportSchedule.ReportStartDate)),
                    new FhirDateTime(new DateTimeOffset(reportSchedule.ReportEndDate))),
                Reporter = new ResourceReference($"Organization/{organizationId}")
            };

            foreach (var groupPopulation in reportPopulation.GroupPopulations)
            {
                CodeableConcept populationCode;
                try
                {
                    populationCode = JsonSerializer.Deserialize<CodeableConcept>(
                        groupPopulation.PopulationCodeJson!,
                        LinkFhirSerializerOptions.ForFhirLenientSerialization)!;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize PopulationCodeJson for population {PopulationId}. Using empty CodeableConcept.", groupPopulation.PopulationId);
                    populationCode = new CodeableConcept();
                }

                var subjectList = new List
                {
                    Id = $"{groupPopulation.PopulationId}-list",
                    Status = List.ListStatus.Current,
                    Mode = ListMode.Snapshot
                };

                foreach (var mrp in groupPopulation.MeasureReportPopulations)
                {
                    subjectList.Entry.Add(new List.EntryComponent
                    {
                        Item = new ResourceReference($"MeasureReport/{mrp.MeasureReportId}")
                    });
                }

                measureReport.Contained.Add(subjectList);

                measureReport.Group.Add(new MeasureReport.GroupComponent
                {
                    Population = new List<MeasureReport.PopulationComponent>
                    {
                        new MeasureReport.PopulationComponent
                        {
                            Code = populationCode,
                            Count = groupPopulation.TotalPopulationCount,
                            SubjectResults = new ResourceReference($"#{groupPopulation.PopulationId}-list")
                        }
                    }
                });
            }

            aggregates.Add(measureReport);
        }

        _logger.LogInformation("Successfully created {Count} aggregate MeasureReport(s) for schedule {ScheduleId}",
            aggregates.Count, reportSchedule.Id);

        return aggregates;
    }
}