using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IBaseEntityRepository<PatientResourceModel> PatientResourceRepository { get; set; }
        IBaseEntityRepository<SharedResourceModel> SharedResourceRepository { get; set; }
        IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        IBaseEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository { get; set; }
    }

    public class Database : IDatabase
    {
        protected IMongoDatabase DbContext { get; set; }

        public IBaseEntityRepository<PatientResourceModel> PatientResourceRepository { get; set; }
        public IBaseEntityRepository<SharedResourceModel> SharedResourceRepository { get; set; }
        public IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; }
        public IBaseEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository { get; set; }

        public Database(IOptions<MongoConnection> mongoSettings,
            IBaseEntityRepository<PatientResourceModel> patientResourceRepository,
            IBaseEntityRepository<SharedResourceModel> sharedResourceRepository,
            IBaseEntityRepository<ReportScheduleModel> reportScheduledRepository,
            IBaseEntityRepository<MeasureReportSubmissionEntryModel> submissionEntryRepository)
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
