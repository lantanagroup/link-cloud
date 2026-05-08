using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace QueryDispatch.Domain
{
    public interface IDatabase
    {
        IBaseEntityRepository<ScheduledReportEntity> ScheduledReportRepo { get; set; }
        IBaseEntityRepository<PatientDispatchEntity> PatientDispatchRepo { get; set; }
        IBaseEntityRepository<QueryDispatchConfigurationEntity> QueryDispatchConfigurationRepo { get; set; }

    }
    public class Database : IDatabase
    {

        public IBaseEntityRepository<ScheduledReportEntity> ScheduledReportRepo { get; set; }
        public IBaseEntityRepository<PatientDispatchEntity> PatientDispatchRepo { get; set; }
        public IBaseEntityRepository<QueryDispatchConfigurationEntity> QueryDispatchConfigurationRepo { get; set; }

        public Database(
            IBaseEntityRepository<ScheduledReportEntity> scheduledReportRepo,
            IBaseEntityRepository<PatientDispatchEntity> patientDispatchRepo,
            IBaseEntityRepository<QueryDispatchConfigurationEntity> queryDispatchConfigurationRepo)
        {
            ScheduledReportRepo = scheduledReportRepo;
            PatientDispatchRepo = patientDispatchRepo;
            QueryDispatchConfigurationRepo = queryDispatchConfigurationRepo;
        }
    }
}
