using LantanaGroup.Link.Report.Entities;
using System.Linq.Expressions;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface ISubmissionEntryManager
    {
        Task<List<MeasureReportSubmissionEntryModel>> FindAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel?> SingleOrDefaultAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel?> SingleAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel?> FirstOrDefaultAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> AddAsync(MeasureReportSubmissionEntryModel entity,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> UpdateAsync(MeasureReportSubmissionEntryModel entity,
            CancellationToken cancellationToken = default);

        Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default);

        Task<int> GetReportInitialPopulationCount(string reportId, CancellationToken cancellationToken = default);
        
        Task<Dictionary<string, int>> GetReportInitialPopulationCountBatch(List<string> reportIds, CancellationToken cancellationToken = default);
    }

    public class SubmissionEntryManager : ISubmissionEntryManager
    {

        private readonly IDatabase _database;

        public SubmissionEntryManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AnyAsync(predicate, cancellationToken);
        }

        public async Task<int> GetReportInitialPopulationCount(string reportId, CancellationToken cancellationToken = default)
        {
            var reportEntries = await _database.SubmissionEntryRepository
                .FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);
            
            //TODO: Eventually may need to check validation results
            var initialPopulationCount = reportEntries.Count(x => 
                x.Status != PatientSubmissionStatus.PendingEvaluation && 
                x.Status != PatientSubmissionStatus.NotReportable);

            return initialPopulationCount;
        }

        public async Task<Dictionary<string, int>> GetReportInitialPopulationCountBatch(List<string> reportIds, CancellationToken cancellationToken = default)
        {
            var reportEntries = await _database.SubmissionEntryRepository
                .FindAsync(x => reportIds.Contains(x.ReportScheduleId), cancellationToken);
            var populationCounts = new Dictionary<string, int>();
            foreach (var reportId in reportIds)
            {
                //TODO: Eventually may need to check validation results
                if (!string.IsNullOrWhiteSpace(reportId))
                    populationCounts.TryAdd(reportId,
                        reportEntries.Count(
                            x => x.ReportScheduleId == reportId &&
                                 x.Status != PatientSubmissionStatus.PendingEvaluation &&
                                 x.Status != PatientSubmissionStatus.NotReportable
                        ));
            }
            
            return populationCounts;
        }

        public async Task<List<MeasureReportSubmissionEntryModel>> FindAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel?> FirstOrDefaultAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FirstOrDefaultAsync(predicate, cancellationToken);
        }


        public async Task<MeasureReportSubmissionEntryModel?> SingleOrDefaultAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel?> SingleAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel> AddAsync(MeasureReportSubmissionEntryModel entity, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AddAsync(entity, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel> UpdateAsync(MeasureReportSubmissionEntryModel entity, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.UpdateAsync(entity, cancellationToken);
        }
    }
}
