using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Report.Data
{
    public interface IDatabase
    {
        IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        IEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        IEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        IEntityRepository<ReportResource> ReportResourceRepository { get; set; }
        IEntityRepository<GroupPopulation> GroupPopulationRepository { get; set; }
        IEntityRepository<MeasureReportPopulation> MeasureReportPopulationRepository { get; set; }

        /// <summary>
        /// Begins a new MongoDB multi-document transaction.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }

    public class Database : IDatabase
    {
        protected ReportDbContext DbContext { get; set; }

        public IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        public IEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        public IEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        public IEntityRepository<ReportResource> ReportResourceRepository { get; set; }
        public IEntityRepository<GroupPopulation> GroupPopulationRepository { get; set; }
        public IEntityRepository<MeasureReportPopulation> MeasureReportPopulationRepository { get; set; }

        public Database(ReportDbContext context,
            IEntityRepository<ReportSchedule> reportScheduledRepository,
            IEntityRepository<ReportEntry> reportEntryRepository,
            IEntityRepository<ReportPopulation> reportPopulationRepository,
            IEntityRepository<ReportResource> reportResourceRepository,
            IEntityRepository<GroupPopulation> groupPopulationRepository,
            IEntityRepository<MeasureReportPopulation> measureReportPopulationRepository)
        {
            DbContext = context;

            ReportScheduledRepository = reportScheduledRepository;
            ReportEntryRepository = reportEntryRepository;
            ReportPopulationRepository = reportPopulationRepository;
            ReportResourceRepository = reportResourceRepository;
            GroupPopulationRepository = groupPopulationRepository;
            MeasureReportPopulationRepository = measureReportPopulationRepository;
        }

        /// <summary>
        /// Begins a new MongoDB multi-document transaction.
        /// </summary>
        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            await DbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            await DbContext.Database.CommitTransactionAsync(cancellationToken);
        }

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            await DbContext.Database.RollbackTransactionAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
