using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
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
        IEntityRepository<ResourceReferenceType> ResourceReferenceTypeRepository { get; set; }
        IEntityRepository<FhirQueryResourceType> FhirQueryResourceTypeRepository { get; set; }
        IEntityRepository<DataAcquisitionLog> DataAcquisitionLogRepository { get; set; }
        IEntityRepository<SftpAcquisitionLog> SftpAcquisitionLogRepository { get; set; }
        IEntityRepository<SftpConfiguration> SftpConfigurationRepository { get; set; }
        IEntityRepository<OrganizationLocationConfiguration> LocationConfigurationRepository { get; set; }
        IEntityRepository<OrganizationLocationCondition> LocationConditionRepository { get; set; }
        IEntityRepository<OrganizationLocationMapping> LocationMappingRepository { get; set; }


        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
        Task SaveChangesAsync();
    }
    public class Database : IDatabase
    {
        private readonly DataAcquisitionDbContext _context;
        public IEntityRepository<QueryPlan> QueryPlanRepository { get; set; }
        public IEntityRepository<FhirQueryConfiguration> FhirQueryConfigurationRepository { get; set; }
        public IEntityRepository<FhirListConfiguration> FhirListConfigurationRepository { get; set; }
        public IEntityRepository<FhirQuery> FhirQueryRepository { get; set; }
        public IEntityRepository<ResourceReferenceType> ResourceReferenceTypeRepository { get; set; }
        public IEntityRepository<ReferenceResources> ReferenceResourcesRepository { get; set; }
        public IEntityRepository<FhirQueryResourceType> FhirQueryResourceTypeRepository { get; set; }
        public IEntityRepository<DataAcquisitionLog> DataAcquisitionLogRepository { get; set; }
        public IEntityRepository<SftpAcquisitionLog> SftpAcquisitionLogRepository { get; set; }
        public IEntityRepository<SftpConfiguration> SftpConfigurationRepository { get; set; }
        public IEntityRepository<OrganizationLocationConfiguration> LocationConfigurationRepository { get; set; }
        public IEntityRepository<OrganizationLocationCondition> LocationConditionRepository { get; set; }
        public IEntityRepository<OrganizationLocationMapping> LocationMappingRepository { get; set; }

        public Database(
            DataAcquisitionDbContext context,
            IEntityRepository<FhirQueryConfiguration> queryConfigurationRepository,
            IEntityRepository<FhirListConfiguration> fhirListQueryListConfigurationRepository,
            IEntityRepository<FhirQuery> fhirQueryRepository,
            IEntityRepository<ReferenceResources> referenceResourcesRepository,
            IEntityRepository<QueryPlan> queryPlans,
            IEntityRepository<DataAcquisitionLog> dataAcquisitionLogRepository,
            IEntityRepository<ResourceReferenceType> resourceReferenceTypeRepository,
            IEntityRepository<FhirQueryResourceType> fhirQueryResourceTypeRepository,
            IEntityRepository<SftpAcquisitionLog> sftpAcquisitionLogRepository,
            IEntityRepository<SftpConfiguration> sftpConfigurationRepository,
            IEntityRepository<OrganizationLocationConfiguration> locationConfigurationRepository,
            IEntityRepository<OrganizationLocationCondition> locationConditionRepository,
            IEntityRepository<OrganizationLocationMapping> locationMappingRepository)
        {
            _context = context;
            QueryPlanRepository = queryPlans;
            FhirQueryConfigurationRepository = queryConfigurationRepository;
            FhirListConfigurationRepository = fhirListQueryListConfigurationRepository;
            FhirQueryRepository = fhirQueryRepository;
            ReferenceResourcesRepository = referenceResourcesRepository;
            DataAcquisitionLogRepository = dataAcquisitionLogRepository;
            ResourceReferenceTypeRepository = resourceReferenceTypeRepository;
            FhirQueryResourceTypeRepository = fhirQueryResourceTypeRepository;
            SftpAcquisitionLogRepository = sftpAcquisitionLogRepository;
            SftpConfigurationRepository = sftpConfigurationRepository;
            LocationConfigurationRepository = locationConfigurationRepository;
            LocationConditionRepository = locationConditionRepository;
            LocationMappingRepository = locationMappingRepository;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            await _context.Database.CommitTransactionAsync(cancellationToken);
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            await _context.Database.RollbackTransactionAsync(cancellationToken);
        }
    }
}