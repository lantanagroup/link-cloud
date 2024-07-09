using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace DataAcquisition.Domain
{
    public interface IDatabase
    {
        IEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        IEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        IEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        IEntityRepository<QueriedFhirResourceRecord> QueriedFhirResourceRepository { get; set; }
        IEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }
    }
    public class Database : IDatabase
    {
        public IEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        public IEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        public IEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        public IEntityRepository<QueriedFhirResourceRecord> QueriedFhirResourceRepository { get; set; }
        public IEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }

        public Database(
            IEntityRepository<FhirQueryConfiguration> queryConfigurationRepository,
            IEntityRepository<FhirListConfiguration> fhirListQueryListConfigurationRepository,
            IEntityRepository<QueriedFhirResourceRecord> queriedFhirResourceRepository,
            IEntityRepository<ReferenceResources> referenceResourcesRepository,
            IEntityRepository<QueryPlan> queryPlans)
        {
            QueryPlanRepository = queryPlans;
            FhirQueryConfigurationRepository = queryConfigurationRepository;
            FhirListConfigurationRepository = fhirListQueryListConfigurationRepository;
            QueriedFhirResourceRepository = queriedFhirResourceRepository;
            ReferenceResourcesRepository = referenceResourcesRepository;
        }
    }
}
