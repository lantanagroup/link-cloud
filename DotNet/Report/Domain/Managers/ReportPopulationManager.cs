using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportPopulationManager
    {
        Task<ReportPopulationModel> UpdateAsync(ReportPopulationModel entry,
            CancellationToken cancellationToken);

        Task<ReportPopulationModel> AddAsync(ReportPopulationModel entry,
            CancellationToken cancellationToken);
        Task<ReportPopulationModel> UpdateAsyncWithAggregateResult(ReportPopulationModel populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<ReportPopulationModel> AddAsyncWithAggregateResult(string facilityId, string reportId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportPopulationModel>> FindAsync(Expression<Func<ReportPopulationModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportPopulationModel?> SingleOrDefaultAsync(
            Expression<Func<ReportPopulationModel, bool>> predicate,
            CancellationToken cancellationToken = default);
    }

    public class ReportPopulationManager : IReportPopulationManager
    {
        private readonly IDatabase _database;

        public ReportPopulationManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportPopulationModel> AddAsync(ReportPopulationModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportPopulationRepository.AddAsync(entry, cancellationToken);
        }

        public async Task<ReportPopulationModel> AddAsyncWithAggregateResult(string facilityId, string reportId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            var populationModel = new ReportPopulationModel()
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

            return await _database.ReportPopulationRepository.AddAsync(populationModel, cancellationToken);
        }

        public async Task<ReportPopulationModel> UpdateAsyncWithAggregateResult(ReportPopulationModel populationModel, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
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

            return await _database.ReportPopulationRepository.UpdateAsync(populationModel, cancellationToken);
        }

        public async Task<List<ReportPopulationModel>> FindAsync(Expression<Func<ReportPopulationModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportPopulationRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportPopulationModel?> SingleOrDefaultAsync(Expression<Func<ReportPopulationModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportPopulationRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportPopulationModel> UpdateAsync(ReportPopulationModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportPopulationRepository.UpdateAsync(entry, cancellationToken);
        }
    }
}
