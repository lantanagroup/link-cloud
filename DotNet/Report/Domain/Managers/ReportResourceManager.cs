using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using System.Diagnostics;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportResourceManager 
    {
        Task<ReportResourceModel> UpdateAsync(ReportResourceModel entry,
            CancellationToken cancellationToken);

        Task<ReportResourceModel> AddAsync(ReportResourceModel entry,
            CancellationToken cancellationToken);

        Task AddAsyncWithAggregateResult(string facilityId, string reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportResourceModel>> FindAsync(Expression<Func<ReportResourceModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportResourceModel?> SingleOrDefaultAsync(
            Expression<Func<ReportResourceModel, bool>> predicate,
            CancellationToken cancellationToken = default);
    }

    public class ReportResourceManager : IReportResourceManager
    {
        private readonly IDatabase _database;

        public ReportResourceManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportResourceModel> AddAsync(ReportResourceModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportResourceRepository.AddAsync(entry, cancellationToken);
        }

        public async Task AddAsyncWithAggregateResult(string facilityId, string reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken)
        {
            foreach (var measureReport in aggregateResult.MeasureReportResults)
            {
                List<ReportResourceModel> resources = new List<ReportResourceModel>();

                foreach (var resource in measureReport.Resources)
                {
                    resources.Add(new ReportResourceModel()
                    {
                        ReportScheduledId = reportId,
                        FacilityId = facilityId,
                        PatientId = patientId,
                        MeasureReportId = measureReport.MeasureReportId,
                        ResourceType = resource[0],
                        ResourceId = resource[1],
                        CreateDate = DateTime.UtcNow
                    });
                }

                await _database.ReportResourceRepository.AddManyAsync(resources, cancellationToken);
            }
        }

        public async Task<List<ReportResourceModel>> FindAsync(Expression<Func<ReportResourceModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportResourceRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportResourceModel?> SingleOrDefaultAsync(Expression<Func<ReportResourceModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportResourceRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportResourceModel> UpdateAsync(ReportResourceModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportResourceRepository.UpdateAsync(entry, cancellationToken);
        }
    }
}
