using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Immutable;

namespace LantanaGroup.Link.Report.Core;

/// <summary>
/// This Class Generates the Aggregate bundle based on the provided individual MeasureReports.
/// These Aggregate Bundles are part of the overall submission step.
/// </summary>
public class MeasureReportAggregator
{
    private const string AggregateMeasureReportProfile =
        "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport";
    
    private readonly ILogger<MeasureReportAggregator> _logger;
    private readonly BlobStorageService _blobStorageService;
    private readonly BlobContainerClient _containerClient;
    private readonly BlobStorageSettings _settings;
    private readonly IDatabase _database;

    public MeasureReportAggregator(ILogger<MeasureReportAggregator> logger, BlobStorageService blobStorageService, IOptions<BlobStorageSettings> settings, IDatabase database)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
        _settings = settings.Value;
        _database = database ?? throw new ArgumentNullException(nameof(database));

        if (_settings.ConnectionString != null)
        {
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }
    }

    public async Task<List<MeasureReport>> CreateMeasureReportAggregate(ReportScheduleModel reportSchedule, string organizationId) 
    {
        var parser = new FhirJsonParser();
        var populationModels = (await _database.ReportPopulationRepository.FindAsync(x => x.ReportScheduleId == reportSchedule.Id));

        List<MeasureReport> aggregates = new List<MeasureReport>();

        foreach (var populationModel in populationModels) {
            MeasureReport measureReport = new MeasureReport();

            measureReport.Meta = new Meta()
            {
                Profile = ImmutableList.Create<string>(AggregateMeasureReportProfile)
            };
            measureReport.Id = Guid.NewGuid().ToString();
            measureReport.Type = MeasureReport.MeasureReportType.SubjectList;
            measureReport.Status = MeasureReport.MeasureReportStatus.Complete;
            measureReport.DateElement = FhirDateTime.Now();
            measureReport.Measure = populationModel.Measure;
            measureReport.Period = new Period(new FhirDateTime(new DateTimeOffset(reportSchedule.ReportStartDate)), new FhirDateTime(new DateTimeOffset(reportSchedule.ReportEndDate)));
            measureReport.Reporter = new ResourceReference($"Organization/{organizationId}");

            foreach (var reportPopulation in populationModel.ReportPopulations) {
                List measureReportList = new List();
                
                foreach (var measureReportPopulation in reportPopulation.MeasureReportIds) {
                    measureReportList.Entry.Add(new List.EntryComponent()
                    {
                        Item = new ResourceReference()
                        {
                            Reference = "MeasureReport/" + measureReportPopulation.MeasureReportId
                        }
                    });
                }

                measureReport.Contained.Add(measureReportList);

                CodeableConcept codeableConcept = reportPopulation.PopulationCode; 
                
                measureReport.Group.Add(new MeasureReport.GroupComponent()
                {
                    Population = new List<MeasureReport.PopulationComponent>() { 
                        new MeasureReport.PopulationComponent() { 
                            Code = codeableConcept,
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