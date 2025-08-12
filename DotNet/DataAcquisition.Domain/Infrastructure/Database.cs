using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure
{
    public interface IDatabase
    {
        IEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        IEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        IEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        IEntityRepository<FhirQuery> FhirQueryRepository { get; set; }
        IEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }
        IEntityRepository<DataAcquisitionLog> DataAcquisitionLogRepository { get; set; }
    }
    public class Database : IDatabase
    {
        public IEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        public IEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        public IEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        public IEntityRepository<FhirQuery> FhirQueryRepository { get; set; }
        public IEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }
        public IEntityRepository<DataAcquisitionLog> DataAcquisitionLogRepository { get; set; }

        public Database(
            IEntityRepository<FhirQueryConfiguration> queryConfigurationRepository,
            IEntityRepository<FhirListConfiguration> fhirListQueryListConfigurationRepository,
            IEntityRepository<FhirQuery> fhirQueryRepository,
            IEntityRepository<ReferenceResources> referenceResourcesRepository,
            IEntityRepository<QueryPlan> queryPlans,
            IEntityRepository<DataAcquisitionLog> dataAcquisitionLogRepository)
        {
            QueryPlanRepository = queryPlans;
            FhirQueryConfigurationRepository = queryConfigurationRepository;
            FhirListConfigurationRepository = fhirListQueryListConfigurationRepository;
            FhirQueryRepository = fhirQueryRepository;
            ReferenceResourcesRepository = referenceResourcesRepository;
            DataAcquisitionLogRepository = dataAcquisitionLogRepository;
        }
    }
}
