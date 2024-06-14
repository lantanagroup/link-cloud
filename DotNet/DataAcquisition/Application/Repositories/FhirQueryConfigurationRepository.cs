using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class FhirQueryConfigurationRepository : BaseSqlConfigurationRepo<FhirQueryConfiguration>, IFhirQueryConfigurationRepository
{
    private readonly ILogger<FhirQueryConfigurationRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public FhirQueryConfigurationRepository(ILogger<FhirQueryConfigurationRepository> logger, DataAcquisitionDbContext dbContext) 
        : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AuthenticationConfiguration> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
        {
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        if (queryResult.Authentication == null)
        {
            throw new NotFoundException($"No Authentication found on configuration for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return queryResult.Authentication;
    }

    public async Task SaveAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");


        queryResult.Authentication = config;
        FhirQueryConfiguration savedConfig = _dbContext.FhirQueryConfigurations.Update(queryResult).Entity;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete authentication settings.");

        entity.Authentication = null;
        _dbContext.FhirQueryConfigurations.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public override async Task<FhirQueryConfiguration> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = await (_dbContext.FhirQueryConfigurations.Where(x => x.FacilityId == facilityId)).FirstOrDefaultAsync();
        return queryResult;
    }

    public override async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration Entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(Entity.FacilityId, cancellationToken);

        if(existingEntity != null)
        {
            existingEntity.Authentication = Entity.Authentication;
            existingEntity.QueryPlanIds = Entity.QueryPlanIds;
            existingEntity.FhirServerBaseUrl = Entity.FhirServerBaseUrl;
            existingEntity.ModifyDate = DateTime.UtcNow;

            _dbContext.FhirQueryConfigurations.Update(existingEntity);
        }
        else
        {
            Entity.Id = Guid.NewGuid();
            Entity.CreateDate = DateTime.UtcNow;
            Entity.ModifyDate = DateTime.UtcNow;
            _dbContext.FhirQueryConfigurations.Add(Entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingEntity ?? Entity;
    }

    public override async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete configuration.");

        _dbContext.FhirQueryConfigurations.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public void Dispose()
    {
        
    }
}
