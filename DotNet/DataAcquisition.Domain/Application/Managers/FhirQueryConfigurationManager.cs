using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryConfigurationManager
{
    Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirQueryConfigurationModel> CreateAsync(CreateFhirQueryConfigurationModel entity, CancellationToken cancellationToken = default);
    Task<FhirQueryConfigurationModel> UpdateAsync(UpdateFhirQueryConfigurationModel entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryConfigurationManager : IFhirQueryConfigurationManager
{
    private readonly IDatabase _database;

    public FhirQueryConfigurationManager(IDatabase database)
    {
        _database = database;
    }
    
    public async Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
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

        return AuthenticationConfigurationModel.FromDomain(queryResult.Authentication);
    }

    public async Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        queryResult.Authentication = config;
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return AuthenticationConfigurationModel.FromDomain(queryResult.Authentication);
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to delete authentication settings.");

        entity.Authentication = null;
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();
    }

    public async Task<FhirQueryConfigurationModel> CreateAsync(CreateFhirQueryConfigurationModel model, CancellationToken cancellationToken = default)
    {
        var existingEntity = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == model.FacilityId);

        if (existingEntity != null)
        {
            throw new EntityAlreadyExistsException(
                $"A {nameof(FhirQueryConfiguration)} already exists for facilityId: {model.FacilityId}");
        }

        if(string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentNullException("FacilityId camnot be null or empty");
        }

        if (string.IsNullOrEmpty(model.FhirServerBaseUrl))
        {
            throw new ArgumentNullException("FhirServerBaseUrl camnot be null or empty");
        }

        var entity = new FhirQueryConfiguration
        {
            Authentication = model.Authentication?.ToDomain(),
            MaxAcquisitionPullTime = model.MaxAcquisitionPullTime,
            MinAcquisitionPullTime = model.MinAcquisitionPullTime,
            FacilityId = model.FacilityId,
            FhirServerBaseUrl = model.FhirServerBaseUrl,
            MaxConcurrentRequests = model.MaxConcurrentRequests,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        await _database.FhirQueryConfigurationRepository.AddAsync(entity);
        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return FhirQueryConfigurationModel.FromDomain(entity);
    }

    public async Task<FhirQueryConfigurationModel> UpdateAsync(UpdateFhirQueryConfigurationModel model, CancellationToken cancellationToken = default)
    {
        var existingEntity = await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == model.FacilityId);

        if (existingEntity == null)
            throw new NotFoundException($"No configuration found for facilityId: {model.FacilityId}. Unable to update configuration.");

        if (string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentNullException("FacilityId camnot be null or empty");
        }

        if (string.IsNullOrEmpty(model.FhirServerBaseUrl))
        {
            throw new ArgumentNullException("FhirServerBaseUrl camnot be null or empty");
        }

        existingEntity.Authentication = model.Authentication?.ToDomain();
        existingEntity.FhirServerBaseUrl = model.FhirServerBaseUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;
        existingEntity.MaxConcurrentRequests = model.MaxConcurrentRequests;
        existingEntity.MinAcquisitionPullTime = model.MinAcquisitionPullTime;
        existingEntity.MaxAcquisitionPullTime = model.MaxAcquisitionPullTime;

        await _database.FhirQueryConfigurationRepository.SaveChangesAsync();

        return FhirQueryConfigurationModel.FromDomain(existingEntity);
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