using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryConfigurationManager
{
    Task<AuthenticationConfiguration?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirQueryConfiguration> AddAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default);
    Task<FhirQueryConfiguration?> GetAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration entity,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryConfigurationManager : IFhirQueryConfigurationManager
{
    private readonly ILogger<FhirQueryConfigurationManager> _logger;
    private readonly IDatabase _database;

    public FhirQueryConfigurationManager(IDatabase database, ILogger<FhirQueryConfigurationManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database;
    }

    public async Task<AuthenticationConfiguration?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
        {
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return queryResult.Authentication;
    }

    public async Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        if (queryResult.Authentication != null)
        {
            throw new EntityAlreadyExistsException(
                $"An AuthenticationConfiguration already exists for the FhirQueryConfiguration for facilityId {facilityId}");
        }

        queryResult.Authentication = config;
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return queryResult.Authentication;
    }

    public async Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        queryResult.Authentication = config;
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return queryResult.Authentication;
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete authentication settings.");

        entity.Authentication = null;
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();
    }

    public async Task<FhirQueryConfiguration> AddAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {
        FhirQueryConfiguration? existingEntity =
            await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == entity.FacilityId);

        if (existingEntity != null)
        {
            throw new EntityAlreadyExistsException(
                $"A {nameof(FhirQueryConfiguration)} already exists for facilityId: {entity.FacilityId}");
        }

        entity.Id = Guid.NewGuid().ToString();
        entity.CreateDate = DateTime.UtcNow;
        entity.ModifyDate = DateTime.UtcNow;
        await _database.FhirQueryConfigurationRepository.AddAsync(entity);

        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return entity;
    }

    public async Task<FhirQueryConfiguration?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId);
    }

    public async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(entity.FacilityId, cancellationToken);
        
        if (existingEntity == null)
            throw new NotFoundException($"No configuration found for facilityId: {entity.FacilityId}. Unable to update configuration.");

        existingEntity.Authentication = entity.Authentication;
        existingEntity.FhirServerBaseUrl = entity.FhirServerBaseUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;
        existingEntity.MaxConcurrentRequests = entity.MaxConcurrentRequests;
        existingEntity.MinAcquisitionPullTime = entity.MinAcquisitionPullTime;
        existingEntity.MaxAcquisitionPullTime = entity.MaxAcquisitionPullTime;

        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return existingEntity;
    }

    public async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete configuration.");

        _database.FhirQueryConfigurationRepository.Remove(entity);
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return true;
    }
}
