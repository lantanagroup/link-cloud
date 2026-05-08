using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        IEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        IEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        IEntityRepository<ReportResource> ReportResourceRepository { get; set; }

        Task SaveChangesAsync();
    }

    public class Database : IDatabase
    {
        protected MongoDbContext DbContext { get; set; }

        public IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        public IEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        public IEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        public IEntityRepository<ReportResource> ReportResourceRepository { get; set; }

        public Database(MongoDbContext context,
            IEntityRepository<ReportSchedule> reportScheduledRepository,
            IEntityRepository<ReportEntry> reportEntryRepository,
            IEntityRepository<ReportPopulation> reportPopulationRepository,
            IEntityRepository<ReportResource> reportResourceRepository)
        {
            DbContext = context;

            ReportScheduledRepository = reportScheduledRepository;
            ReportEntryRepository = reportEntryRepository;
            ReportPopulationRepository = reportPopulationRepository;
            ReportResourceRepository = reportResourceRepository;
        }

        public async Task SaveChangesAsync()
        {
            await DbContext.SaveChangesAsync();
        }
    }
}
