using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace QueryDispatch.Domain
{
    public interface IDatabase
    {
        IEntityRepository<ScheduledReportEntity> ScheduledReportRepo { get; set; }
        IEntityRepository<PatientDispatchEntity> PatientDispatchRepo { get; set; }
        IEntityRepository<QueryDispatchConfigurationEntity> QueryDispatchConfigurationRepo { get; set; }

    }
    public class Database : IDatabase
    {

        public IEntityRepository<ScheduledReportEntity> ScheduledReportRepo { get; set; }
        public IEntityRepository<PatientDispatchEntity> PatientDispatchRepo { get; set; }
        public IEntityRepository<QueryDispatchConfigurationEntity> QueryDispatchConfigurationRepo { get; set; }

        public Database(
            IEntityRepository<ScheduledReportEntity> scheduledReportRepo,
            IEntityRepository<PatientDispatchEntity> patientDispatchRepo,
            IEntityRepository<QueryDispatchConfigurationEntity> queryDispatchConfigurationRepo)
        {
            ScheduledReportRepo = scheduledReportRepo;
            PatientDispatchRepo = patientDispatchRepo;
            QueryDispatchConfigurationRepo = queryDispatchConfigurationRepo;
        }
    }
}
