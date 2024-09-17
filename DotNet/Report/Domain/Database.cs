using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IEntityRepository<PatientResourceModel> PatientResourceRepository { get; set; }
        IEntityRepository<SharedResourceModel> SharedResourceRepository { get; set; }
        IEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        IEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository { get; set; }
    }

    public class Database : IDatabase
    {
        protected IMongoDatabase DbContext { get; set; }

        public IEntityRepository<PatientResourceModel> PatientResourceRepository { get; set; }
        public IEntityRepository<SharedResourceModel> SharedResourceRepository { get; set; }
        public IEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        public IEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository { get; set; }

        public Database(IOptions<MongoConnection> mongoSettings,
            IEntityRepository<PatientResourceModel> patientResourceRepository,
            IEntityRepository<SharedResourceModel> sharedResourceRepository,
            IEntityRepository<ReportScheduleModel> reportScheduledRepository,
            IEntityRepository<MeasureReportSubmissionEntryModel> submissionEntryRepository)
        {
            var client = new MongoClient(mongoSettings.Value.ConnectionString);
            DbContext = client.GetDatabase(mongoSettings.Value.DatabaseName);

            PatientResourceRepository = patientResourceRepository;
            SharedResourceRepository = sharedResourceRepository;
            ReportScheduledRepository = reportScheduledRepository;
            SubmissionEntryRepository = submissionEntryRepository;
        }
    }
}
