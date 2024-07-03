using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class FhirQueryConfigurationRepository : EntityRepository<FhirQueryConfiguration>, IFhirQueryConfigurationRepository
{
    private readonly ILogger<FhirQueryConfigurationRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public FhirQueryConfigurationRepository(ILogger<FhirQueryConfigurationRepository> logger, DataAcquisitionDbContext dbContext) 
        : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AuthenticationConfiguration?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
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

    public async Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
            throw new MissingFacilityConfigurationException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        if (queryResult.Authentication != null)
        {
            throw new EntityAlreadyExistsException(
                $"An AuthenticationConfiguration already exists for the FhirQueryConfiguration for facilityId {facilityId}");
        }

        queryResult.Authentication = config;
        var savedConfig = _dbContext.FhirQueryConfigurations.Update(queryResult).Entity;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return config;
    }

    public async Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
            throw new MissingFacilityConfigurationException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");


        queryResult.Authentication = config;
        var savedConfig = _dbContext.FhirQueryConfigurations.Update(queryResult).Entity;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return config;
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

    public override async Task<FhirQueryConfiguration> AddAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {
        FhirQueryConfiguration? existingEntity =
            await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(x => x.FacilityId == entity.FacilityId,
                cancellationToken);

        if (existingEntity != null)
        {
            throw new EntityAlreadyExistsException(
                $"A {nameof(FhirQueryConfiguration)} already exists for facilityId: {entity.FacilityId}");
        }

        entity.Id = Guid.NewGuid().ToString();
        entity.CreateDate = DateTime.UtcNow;
        entity.ModifyDate = DateTime.UtcNow;
        await _dbContext.FhirQueryConfigurations.AddAsync(entity, cancellationToken);
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public override async Task<FhirQueryConfiguration> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FhirQueryConfigurations.FirstOrDefaultAsync(q => q.FacilityId == id, cancellationToken);
    }

    public override async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(entity.FacilityId, cancellationToken);

        existingEntity.Authentication = entity.Authentication;
        existingEntity.QueryPlanIds = entity.QueryPlanIds;
        existingEntity.FhirServerBaseUrl = entity.FhirServerBaseUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        _dbContext.FhirQueryConfigurations.Update(existingEntity);
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingEntity;
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
}
