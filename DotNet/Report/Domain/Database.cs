using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Report.Domain
{
    public interface IDatabase
    {
        IEntityRepository<FhirResource> ResourceRepository { get; set; }
        IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        IEntityRepository<PatientSubmissionEntry> SubmissionEntryRepository { get; set; }
        IEntityRepository<PatientSubmissionEntryResourceMap> PatientSubmissionEntryResourceMapRepository { get; set; }

        Task SaveChangesAsync();
    }

    public class Database : IDatabase
    {
        protected MongoDbContext DbContext { get; set; }

        public IEntityRepository<FhirResource> ResourceRepository { get; set; }
        public IEntityRepository<ReportSchedule> ReportScheduledRepository { get; set; }
        public IEntityRepository<PatientSubmissionEntry> SubmissionEntryRepository { get; set; }
        public IEntityRepository<PatientSubmissionEntryResourceMap> PatientSubmissionEntryResourceMapRepository { get; set; }

        public Database(MongoDbContext context,
            IEntityRepository<FhirResource> resourceRepository,
            IEntityRepository<ReportSchedule> reportScheduledRepository,
            IEntityRepository<PatientSubmissionEntry> submissionEntryRepository,
            IEntityRepository<PatientSubmissionEntryResourceMap> reportScheduleResourceMapRepository)
        {
            DbContext = context;

            ResourceRepository = resourceRepository;
            ReportScheduledRepository = reportScheduledRepository;
            SubmissionEntryRepository = submissionEntryRepository;
            PatientSubmissionEntryResourceMapRepository = reportScheduleResourceMapRepository;
        }

        public async Task SaveChangesAsync()
        {
            await DbContext.SaveChangesAsync();
        }
    }
}
