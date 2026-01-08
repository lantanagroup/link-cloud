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
        IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        IBaseEntityRepository<ReportEntryModel> ReportEntryRepository { get; set; }
        IBaseEntityRepository<ReportPopulationModel> ReportPopulationRepository { get; set; }
        MongoEntityRepository<ReportResourceModel> ReportResourceRepository { get; set; }
    }

    public class Database : IDatabase
    {
        protected IMongoDatabase DbContext { get; set; }

        public IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        public IBaseEntityRepository<ReportEntryModel> ReportEntryRepository { get; set; }
        public IBaseEntityRepository<ReportPopulationModel> ReportPopulationRepository { get; set; }
        public MongoEntityRepository<ReportResourceModel> ReportResourceRepository { get; set; }

        public Database(IOptions<MongoConnection> mongoSettings,
            IBaseEntityRepository<ReportScheduleModel> reportScheduledRepository,
            IBaseEntityRepository<ReportEntryModel> reportEntryRepository,
            IBaseEntityRepository<ReportPopulationModel> reportPopulationRepository,
            MongoEntityRepository<ReportResourceModel> reportResourceRepository)
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
