using Hl7.Fhir.Model;
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
    
    public MeasureReportAggregator(ILogger<MeasureReportAggregator> logger)
    {
        _logger = logger;
    }

    public List<MeasureReport> Aggregate(List<MeasureReport> individuals, string organizationId, DateTime startDate, DateTime endDate)
    {
        List<MeasureReport> aggregates = new List<MeasureReport>();

        foreach (MeasureReport indMeasureReport in individuals)
        {
            MeasureReport? aggregate = aggregates.FirstOrDefault(a => a.Measure == indMeasureReport.Measure);

            if (aggregate is null)
            {
                aggregate = new MeasureReport();
                aggregate.Meta = new Meta()
                {
                    Profile = ImmutableList.Create<string>(AggregateMeasureReportProfile)
                };
                aggregate.Id = Guid.NewGuid().ToString();
                aggregate.Type = MeasureReport.MeasureReportType.SubjectList;
                aggregate.Status = MeasureReport.MeasureReportStatus.Complete;
                aggregate.DateElement = FhirDateTime.Now();
                aggregate.Measure = indMeasureReport.Measure;
                aggregate.Period = new Period(new FhirDateTime(new DateTimeOffset(startDate)), new FhirDateTime(new DateTimeOffset(endDate)));
                aggregate.Reporter = new ResourceReference($"Organization/{organizationId}");
                aggregates.Add(aggregate);
            }

            if (indMeasureReport.Group is null || indMeasureReport.Group.Count == 0)
                _logger.LogWarning("MeasureReport {0} has no groups", indMeasureReport.Id);

            foreach (MeasureReport.GroupComponent indGroup in indMeasureReport.Group)
            {
                if (indGroup.Population is null || indGroup.Population.Count == 0)
                    Log.Logger.Warning("MeasureReport {0} group {1} has no populations", indMeasureReport.Id, indGroup.Code?.Coding[0]?.Code);
                
                foreach (MeasureReport.PopulationComponent indPopulation in indGroup.Population)
                {
                    MeasureReport.PopulationComponent aggregatePopulation = GetOrCreateGroupAndPopulation(aggregate, indGroup, indPopulation);
                    List list = GetOrCreateContainedList(aggregate, aggregatePopulation);
                    
                    aggregatePopulation.Count += indPopulation.Count;
                    list.Entry.Add(new List.EntryComponent()
                    {
                        Item = new ResourceReference("MeasureReport/" + indMeasureReport.Id)
                    });
                }
            }
        }

        return aggregates;
    }

    private List GetOrCreateContainedList(MeasureReport aggregate, MeasureReport.PopulationComponent population)
    {
        if (aggregate is null)
            throw new ArgumentNullException(nameof(aggregate));
        
        if (population is null)
            throw new ArgumentNullException(nameof(population));

        String listId = population.SubjectResults?.Reference?.Substring(1) ?? 
                        population.Code?.Coding[0]?.Code + "-list" ?? 
                        throw new ArgumentException("population is missing code.coding[0].code");
        
        List list = aggregate.Contained.FirstOrDefault(c => c.Id == listId) as List;

        if (list is null)
        {
            list = new List();
            list.Id = listId;
            list.Entry = new List<List.EntryComponent>();
            aggregate.Contained.Add(list);
            population.SubjectResults = new ResourceReference("#" + listId);
        }

        return list;
    }
    
    private MeasureReport.PopulationComponent GetOrCreateGroupAndPopulation(MeasureReport aggregate, MeasureReport.GroupComponent indGroup, MeasureReport.PopulationComponent indPopulation)
    {
        if (aggregate is null)
        {
            throw new ArgumentNullException("aggregate");
        }
        
        // get the population and group codes
        string populationCode = (indPopulation.Code != null && indPopulation.Code.Coding.Count > 0) ? indPopulation.Code.Coding[0].Code : "";
        string groupCode = (indGroup.Code != null && indGroup.Code.Coding.Count > 0) ? indGroup.Code.Coding[0].Code : "";

        MeasureReport.GroupComponent aggregateGroup = null;
        MeasureReport.PopulationComponent aggregatePopulation;
        
        // find the group by code
        var foundAggregateGroup = aggregate.Group.FirstOrDefault(g => g.Code != null && g.Code.Coding.Count > 0 && g.Code.Coding[0].Code == groupCode);
        
        // if empty find the group without the code
        if (foundAggregateGroup != null)
        {
            aggregateGroup = foundAggregateGroup;
        }
        else
        {
            if (groupCode == "")
            {
                aggregateGroup = (aggregate.Group != null && aggregate.Group.Count > 0) ? aggregate.Group[0] : null; // only one group with no code
            }
        }
        
        // if still empty create it
        if (aggregateGroup == null)
        {
            aggregateGroup = new MeasureReport.GroupComponent();
            aggregateGroup.Code = (indGroup.Code != null ? indGroup.Code : null);
            aggregate.Group.Add(aggregateGroup);
        }
        
        // find population by code
        MeasureReport.PopulationComponent foundAggregatePopulation = aggregateGroup.Population.FirstOrDefault(g => g.Code != null && g.Code.Coding.Count > 0 && g.Code.Coding[0].Code == populationCode);
        
        // if population not found, create it
        if (foundAggregatePopulation != null)
        {
            aggregatePopulation = foundAggregatePopulation;
        }
        else
        {
            aggregatePopulation = new MeasureReport.PopulationComponent();
            aggregatePopulation.Code = indPopulation.Code;
            aggregatePopulation.Count = 0;
            aggregateGroup.Population.Add(aggregatePopulation);
        }
        
        return aggregatePopulation;
    }
}