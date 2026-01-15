using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using System.Diagnostics;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportResourceManager
    {
        Task<ReportResource> UpdateAsync(ReportResource entry,
            CancellationToken cancellationToken);

        Task<ReportResource> AddAsync(ReportResource entry,
            CancellationToken cancellationToken);

        Task AddAsyncWithAggregateResult(string facilityId, string reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportResource>> FindAsync(Expression<Func<ReportResource, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportResource?> SingleOrDefaultAsync(
            Expression<Func<ReportResource, bool>> predicate,
            CancellationToken cancellationToken = default);
    }

    public class ReportResourceManager : IReportResourceManager
    {
        private readonly IDatabase _database;

        public ReportResourceManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportResource> AddAsync(ReportResource entry, CancellationToken cancellationToken)
        {
            await _database.ReportResourceRepository.AddAsync(entry, cancellationToken);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task AddAsyncWithAggregateResult(string facilityId, string reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken)
        {
            foreach (var measureReport in aggregateResult.MeasureReportResults)
            {
                List<ReportResource> resources = new List<ReportResource>();

                foreach (var resource in measureReport.Resources)
                {
                    resources.Add(new ReportResource()
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

                await _database.ReportResourceRepository.AddRangeAsync(resources);
                await _database.SaveChangesAsync();
            }
        }

        public async Task<List<ReportResource>> FindAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportResourceRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportResource?> SingleOrDefaultAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportResourceRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportResource> UpdateAsync(ReportResource entry, CancellationToken cancellationToken)
        {
            _database.ReportResourceRepository.Update(entry);
            await _database.SaveChangesAsync();

            return entry;
        }
    }
}