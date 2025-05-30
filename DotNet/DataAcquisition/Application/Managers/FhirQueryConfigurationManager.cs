using DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

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
        var queryResult = await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
        {
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return queryResult.Authentication;
    }

    public async Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        if (queryResult.Authentication != null)
        {
            throw new EntityAlreadyExistsException(
                $"An AuthenticationConfiguration already exists for the FhirQueryConfiguration for facilityId {facilityId}");
        }

        queryResult.Authentication = config;
        var savedConfig = await _database.FhirQueryConfigurationRepository.UpdateAsync(queryResult, cancellationToken);

        return savedConfig.Authentication;
    }

    public async Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        queryResult.Authentication = config;
        var savedConfig = await _database.FhirQueryConfigurationRepository.UpdateAsync(queryResult, cancellationToken);

        return savedConfig.Authentication;
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete authentication settings.");

        entity.Authentication = null;
        await _database.FhirQueryConfigurationRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<FhirQueryConfiguration> AddAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {
        FhirQueryConfiguration? existingEntity =
            await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == entity.FacilityId,
                cancellationToken);

        if (existingEntity != null)
        {
            throw new EntityAlreadyExistsException(
                $"A {nameof(FhirQueryConfiguration)} already exists for facilityId: {entity.FacilityId}");
        }

        entity.Id = Guid.NewGuid().ToString();
        entity.CreateDate = DateTime.UtcNow;
        entity.ModifyDate = DateTime.UtcNow;
        await _database.FhirQueryConfigurationRepository.AddAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<FhirQueryConfiguration?> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId, cancellationToken);
    }

    public async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(entity.FacilityId, cancellationToken);

        existingEntity.Authentication = entity.Authentication;
        existingEntity.FhirServerBaseUrl = entity.FhirServerBaseUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        return await _database.FhirQueryConfigurationRepository.UpdateAsync(existingEntity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete configuration.");

        await _database.FhirQueryConfigurationRepository.RemoveAsync(entity);
        
        return true;
    }
}
