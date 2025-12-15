using Hl7.Fhir.Model;
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
