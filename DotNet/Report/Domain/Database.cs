using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        IBaseEntityRepository<ReportEntryStatusModel> ReportEntryStatusRepository { get; set; }
        IBaseEntityRepository<ReportPopulationModel> ReportPopulationRepository { get; set; }
    }

    public class Database : IDatabase
    {
        protected IMongoDatabase DbContext { get; set; }

        public IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        public IBaseEntityRepository<ReportEntryStatusModel> ReportEntryStatusRepository { get; set; }
        public IBaseEntityRepository<ReportPopulationModel> ReportPopulationRepository { get; set; }

        public Database(IOptions<MongoConnection> mongoSettings,
            IBaseEntityRepository<ReportScheduleModel> reportScheduledRepository,
            IBaseEntityRepository<ReportEntryStatusModel> reportEntryStatusRepository,
            IBaseEntityRepository<ReportPopulationModel> reportPopulationRepository)
        {
            var client = new MongoClient(mongoSettings.Value.ConnectionString);
            DbContext = client.GetDatabase(mongoSettings.Value.DatabaseName);
            ReportScheduledRepository = reportScheduledRepository;
            ReportEntryStatusRepository = reportEntryStatusRepository;
            ReportPopulationRepository = reportPopulationRepository;
        }
    }
}
