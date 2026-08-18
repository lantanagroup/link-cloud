using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        IEntityRepository<EncounterMapping> EncounterMappingRepository { get; set; }
        IEntityRepository<EncounterLocation> EncounterLocationRepository { get; set; }


        /// <summary>
        /// Runs <paramref name="operation"/> inside a database transaction using the context's
        /// configured execution strategy, so it is safe to use with connection-resiliency
        /// (EnableRetryOnFailure). The whole unit is retried atomically on transient failure;
        /// any exception rolls the transaction back and is rethrown.
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

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
        public IEntityRepository<EncounterMapping> EncounterMappingRepository { get; set; }
        public IEntityRepository<EncounterLocation> EncounterLocationRepository { get; set; }
        
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
            IEntityRepository<OrganizationLocationMapping> locationMappingRepository,
            IEntityRepository<EncounterMapping> encounterMappingRepository,
            IEntityRepository<EncounterLocation> encounterLocationRepository)
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
            EncounterMappingRepository = encounterMappingRepository;
            EncounterLocationRepository = encounterLocationRepository;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Each attempt starts from a clean slate so a retry re-reads state instead of
                // reusing entities mutated by a prior failed attempt.
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await operation();
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
    }
}