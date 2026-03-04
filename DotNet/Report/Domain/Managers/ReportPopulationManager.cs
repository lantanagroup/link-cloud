using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportPopulationManager
    {
        Task<ReportPopulation> UpdateAsync(ReportPopulation entry,
            CancellationToken cancellationToken);

        Task<ReportPopulation> AddAsync(ReportPopulation entry,
            CancellationToken cancellationToken);
        Task<List<ReportPopulation>> AddWithReportScheduleAsync(ReportSchedule reportSchedule, CancellationToken cancellationToken);
        Task<ReportPopulation> UpdateAsyncWithAggregateResult(ReportPopulation populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<ReportPopulation> AddAsyncWithAggregateResult(string facilityId, string reportId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportPopulation>> FindAsync(Expression<Func<ReportPopulation, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportPopulation?> SingleOrDefaultAsync(
            Expression<Func<ReportPopulation, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<int> CountNumberOfMeasureReportPopulationsInIP(string reportScheduleId,
            CancellationToken cancellationToken = default);
    }

    public class ReportPopulationManager : IReportPopulationManager
    {
        private readonly IDatabase _database;

        public ReportPopulationManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportPopulation> AddAsync(ReportPopulation entry, CancellationToken cancellationToken)
        {
            await _database.ReportPopulationRepository.AddAsync(entry, cancellationToken);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task<List<ReportPopulation>> AddWithReportScheduleAsync(ReportSchedule reportSchedule, CancellationToken cancellationToken)
        {
            /*
             * Daniel - 3/2026: Adding this function to build the population list when a reportScheduled event is consumed. This helps in a few ways:
             *  1. In the case where all evaluated measure reports for a facility do not meet the criteria of the measure, a population record already exists with a count set to 0.
             *  2. When generating a manifest, logic doesn't need to be added to figure out if there was a 0 count for any of the reported measures. 
             * It's assumed that each developed measure will at least have 'initial-population' as a Population Id. This logic would need to change if that assumption is no longer true.
            */
            List<ReportPopulation> reportPopulations = new List<ReportPopulation>();

            foreach (var reportType in reportSchedule.ReportTypes)
            {
                ReportPopulation newPopulation = new ReportPopulation()
                {
                    CreateDate = DateTime.UtcNow,
                    FacilityId = reportSchedule.FacilityId,
                    ReportType = reportType,
                    ReportScheduleId = reportSchedule.Id,
                    GroupPopulations = new List<GroupPopulation>()
                    {
                        new GroupPopulation() { 
                            PopulationId = "initial-population",
                            TotalPopulationCount = 0
                        }
                    }
                };

                reportPopulations.Add(newPopulation);
            }

            await _database.ReportPopulationRepository.AddRangeAsync(reportPopulations);
            await _database.SaveChangesAsync();

            return reportPopulations;
        }

        public async Task<ReportPopulation> AddAsyncWithAggregateResult(string facilityId, string reportId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            var populationModel = new ReportPopulation()
            {
                Measure = aggregateResult.Measure,
                ReportType = aggregateResult.ReportType,
                CreateDate = DateTime.UtcNow,
                FacilityId = facilityId,
                ReportScheduleId = reportId
            };

            foreach (var measureReportpopulation in aggregateResult.PopulationList)
            {
                GroupPopulation group = new GroupPopulation()
                {
                    PopulationId = measureReportpopulation.PopulationId,
                    PopulationCode = measureReportpopulation.PopulationCode,
                    TotalPopulationCount = measureReportpopulation.PopulationCount,
                    MeasureReportPopulations = new List<MeasureReportPopulation>() {
                        new MeasureReportPopulation() {
                            MeasureReportId = aggregateResult.MeasureReportId,
                            PopulationCount = measureReportpopulation.PopulationCount
                        }
                    }
                };

                populationModel.GroupPopulations.Add(group);
            }

            await _database.ReportPopulationRepository.AddAsync(populationModel, cancellationToken);
            await _database.SaveChangesAsync();

            return populationModel;
        }

        public async Task<ReportPopulation> UpdateAsyncWithAggregateResult(ReportPopulation populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(populationModel.Measure))
            {
                populationModel.Measure = aggregateResult.Measure;
            }

            foreach (var measureReportpopulation in aggregateResult.PopulationList)
            {
                var group = populationModel.GroupPopulations.FirstOrDefault(x => x.PopulationId == measureReportpopulation.PopulationId);

                if (group == null)
                {
                    group = new GroupPopulation()
                    {
                        PopulationId = measureReportpopulation.PopulationId,
                        PopulationCode = measureReportpopulation.PopulationCode,
                        TotalPopulationCount = measureReportpopulation.PopulationCount,
                        MeasureReportPopulations = new List<MeasureReportPopulation>() {
                            new MeasureReportPopulation() {
                                MeasureReportId = aggregateResult.MeasureReportId,
                                PopulationCount = measureReportpopulation.PopulationCount
                            }
                        }
                    };

                    populationModel.GroupPopulations.Add(group);
                }
                else
                {
                    group.TotalPopulationCount += measureReportpopulation.PopulationCount;
                    group.MeasureReportPopulations.Add(new MeasureReportPopulation()
                    {
                        MeasureReportId = aggregateResult.MeasureReportId,
                        PopulationCount = measureReportpopulation.PopulationCount
                    });
                }
            }

            _database.ReportPopulationRepository.Update(populationModel);
            await _database.SaveChangesAsync();

            return populationModel;
        }

        public async Task<List<ReportPopulation>> FindAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportPopulationRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportPopulation?> SingleOrDefaultAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportPopulationRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportPopulation> UpdateAsync(ReportPopulation entry, CancellationToken cancellationToken)
        {
            _database.ReportPopulationRepository.Update(entry);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task<int> CountNumberOfMeasureReportPopulationsInIP(string reportScheduleId, CancellationToken cancellationToken = default)
        {
            var reportPopulations = await _database.ReportPopulationRepository.FindAsync(
                x => x.ReportScheduleId == reportScheduleId, 
                cancellationToken);

            var count = reportPopulations
                .SelectMany(rp => rp.GroupPopulations)
                .Where(gp => gp.PopulationId == "initial-population")
                .SelectMany(gp => gp.MeasureReportPopulations)
                .Count();

            return count;
        }
    }
}