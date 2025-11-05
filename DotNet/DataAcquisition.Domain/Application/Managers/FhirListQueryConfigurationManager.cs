using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirListQueryConfigurationManager
{
    Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirListConfigurationModel> CreateAsync(CreateFhirListConfigurationModel entity, CancellationToken cancellationToken = default);
    Task<FhirListConfigurationModel> UpdateAsync(UpdateFhirListConfigurationModel entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirListQueryConfigurationManager : IFhirListQueryConfigurationManager
{
    private readonly IDatabase _database;

    public FhirListQueryConfigurationManager(IDatabase database)
    {
        _database = database;
    }

    public async Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId);

        if (queryResult == null)
            throw new MissingFacilityConfigurationException(
                $"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        if (queryResult.Authentication != null)
        {
            throw new EntityAlreadyExistsException(
                $"An AuthenticationConfiguration already exists for the FhirQueryConfiguration for facilityId {facilityId}");
        }

        queryResult.Authentication = config;
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return AuthenticationConfigurationModel.FromDomain(queryResult.Authentication);
    }

    public async Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId);

        if (queryResult == null)
            throw new MissingFacilityConfigurationException(
                $"No configuration found for facilityId: {facilityId}. Unable to save authentication settings.");

        if (queryResult.Authentication == null)
        {
            throw new NotFoundException(
                $"No AuthenticationConfiguration found for the FhirQueryConfiguration for facilityId {facilityId}");
        }

        queryResult.Authentication = config;
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return AuthenticationConfigurationModel.FromDomain(queryResult.Authentication);
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(fl => fl.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException();

        entity.Authentication = null;
        await _database.FhirListConfigurationRepository.SaveChangesAsync();
    }

    public async Task<FhirListConfigurationModel> CreateAsync(CreateFhirListConfigurationModel model, CancellationToken cancellationToken = default)
    {
        if (await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(l => l.FacilityId == model.FacilityId) != null)
            throw new EntityAlreadyExistsException(
                $"A FhirListConfiguration already exists for facilityId: {model.FacilityId?.SanitizeAndRemove()}");

        if(string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentNullException("FacilityId cannot be null");
        }

        if (string.IsNullOrEmpty(model.FhirBaseServerUrl))
        {
            throw new ArgumentNullException("FhirBaseServerUrl cannot be null");
        }

        var entity = new FhirListConfiguration()
        {
            EHRPatientLists = model.EHRPatientLists.Select(e => new EhrPatientList
            {
                FhirId = e.FhirId,
                InternalId = e.InternalId,
                Status = e.Status,
                TimeFrame = e.TimeFrame,
            }).ToList(),
            FhirBaseServerUrl = model.FhirBaseServerUrl,
            FacilityId = model.FacilityId,
            Authentication = model.Authentication?.ToDomain(),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
        };

        var newEntity = await _database.FhirListConfigurationRepository.AddAsync(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return FhirListConfigurationModel.FromDomain(newEntity);
    }

    public async Task<FhirListConfigurationModel> UpdateAsync(UpdateFhirListConfigurationModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentNullException("FacilityId cannot be null");
        }

        if (string.IsNullOrEmpty(model.FhirBaseServerUrl))
        {
            throw new ArgumentNullException("FhirBaseServerUrl cannot be null");
        }

        var existingEntity = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == model.FacilityId);

        if (existingEntity == null)
            throw new MissingFacilityConfigurationException();



        existingEntity.Authentication = model.Authentication?.ToDomain();
        existingEntity.EHRPatientLists = model.EHRPatientLists.Select(e => new EhrPatientList
        {
            FhirId = e.FhirId,
            InternalId = e.InternalId,
            Status = e.Status,
            TimeFrame = e.TimeFrame,
        }).ToList();
        existingEntity.FacilityId = model.FacilityId;
        existingEntity.FhirBaseServerUrl = model.FhirBaseServerUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return FhirListConfigurationModel.FromDomain(existingEntity);
    }

    public async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirListConfigurationRepository.SingleAsync(q => q.FacilityId == facilityId);

        _database.FhirListConfigurationRepository.Remove(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return true;
    }
}