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
        Task<ReportPopulation> UpdateAsyncWithAggregateResult(ReportPopulation populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<ReportPopulation> AddAsyncWithAggregateResult(string facilityId, string reportId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportPopulation>> FindAsync(Expression<Func<ReportPopulation, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportPopulation?> SingleOrDefaultAsync(
            Expression<Func<ReportPopulation, bool>> predicate,
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
                    GroupPopulationMeasureReportList = new List<GroupPopulationMeasureReport>() {
                        new GroupPopulationMeasureReport() {
                            MeasureReportId = aggregateResult.MeasureReportId,
                            PopulationCount = measureReportpopulation.PopulationCount
                        }
                    }
                };

                populationModel.GroupPopulationList.Add(group);
            }

            await _database.ReportPopulationRepository.AddAsync(populationModel, cancellationToken);
            await _database.SaveChangesAsync();

            return populationModel;
        }

        public async Task<ReportPopulation> UpdateAsyncWithAggregateResult(ReportPopulation populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            foreach (var measureReportpopulation in aggregateResult.PopulationList)
            {
                var group = populationModel.GroupPopulationList.FirstOrDefault(x => x.PopulationId == measureReportpopulation.PopulationId);

                if (group == null)
                {
                    group = new GroupPopulation()
                    {
                        PopulationId = measureReportpopulation.PopulationId,
                        PopulationCode = measureReportpopulation.PopulationCode,
                        TotalPopulationCount = measureReportpopulation.PopulationCount,
                        GroupPopulationMeasureReportList = new List<GroupPopulationMeasureReport>() {
                            new GroupPopulationMeasureReport() {
                                MeasureReportId = aggregateResult.MeasureReportId,
                                PopulationCount = measureReportpopulation.PopulationCount
                            }
                        }
                    };

                    populationModel.GroupPopulationList.Add(group);
                }
                else
                {
                    group.TotalPopulationCount += measureReportpopulation.PopulationCount;
                    group.GroupPopulationMeasureReportList.Add(new GroupPopulationMeasureReport()
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
    }
}