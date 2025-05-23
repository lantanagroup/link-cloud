using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace DataAcquisition.Domain
{
    public interface IDatabase
    {
        IBaseEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        IBaseEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        IBaseEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        IBaseEntityRepository<FhirQuery> FhirQueryRepository { get; set; }
        IBaseEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }
    }
    public class Database : IDatabase
    {
        public IBaseEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        public IBaseEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        public IBaseEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        public IBaseEntityRepository<FhirQuery> FhirQueryRepository { get; set; }
        public IBaseEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }

        public Database(
            IBaseEntityRepository<FhirQueryConfiguration> queryConfigurationRepository,
            IBaseEntityRepository<FhirListConfiguration> fhirListQueryListConfigurationRepository,
            IBaseEntityRepository<FhirQuery> fhirQueryRepository,
            IBaseEntityRepository<ReferenceResources> referenceResourcesRepository,
            IBaseEntityRepository<QueryPlan> queryPlans)
        {
            QueryPlanRepository = queryPlans;
            FhirQueryConfigurationRepository = queryConfigurationRepository;
            FhirListConfigurationRepository = fhirListQueryListConfigurationRepository;
            FhirQueryRepository = fhirQueryRepository;
            ReferenceResourcesRepository = referenceResourcesRepository;
        }
    }
}
