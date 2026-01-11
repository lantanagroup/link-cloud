using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IBaseEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        IBaseEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        IBaseEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        MongoEntityRepository<ReportResource> ReportResourceRepository { get; set; }
    }

    public class Database : IDatabase
    {
        protected IMongoDatabase DbContext { get; set; }

        public IBaseEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        public IBaseEntityRepository<ReportEntry> ReportEntryRepository { get; set; }
        public IBaseEntityRepository<ReportPopulation> ReportPopulationRepository { get; set; }
        public MongoEntityRepository<ReportResource> ReportResourceRepository { get; set; }

        public Database(IOptions<MongoConnection> mongoSettings,
            IBaseEntityRepository<ReportSchedule> reportScheduledRepository,
            IBaseEntityRepository<ReportEntry> reportEntryRepository,
            IBaseEntityRepository<ReportPopulation> reportPopulationRepository,
            MongoEntityRepository<ReportResource> reportResourceRepository)
        {
            var client = new MongoClient(mongoSettings.Value.ConnectionString);
            DbContext = client.GetDatabase(mongoSettings.Value.DatabaseName);
            ReportScheduledRepository = reportScheduledRepository;
            ReportEntryRepository = reportEntryRepository;
            ReportPopulationRepository = reportPopulationRepository;
            ReportResourceRepository = reportResourceRepository;
        }
    }
}
