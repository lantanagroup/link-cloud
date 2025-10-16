using AngleSharp.Dom;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryListConfigurationManager
{
    Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirListConfigurationModel> AddAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default);
    Task<FhirListConfigurationModel> UpdateAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirListQueryConfigurationManager : IFhirQueryListConfigurationManager
{
    private readonly ILogger<FhirListQueryConfigurationManager> _logger;
    private readonly IDatabase _database;
    private readonly IFhirQueryListConfigurationQueries _queries;

    public FhirListQueryConfigurationManager(ILogger<FhirListQueryConfigurationManager> logger, IDatabase database, IFhirQueryListConfigurationQueries queries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database;
        _queries = queries;
    }

    public async Task<AuthenticationConfigurationModel> CreateAuthenticationConfiguration(string facilityId,
        AuthenticationConfiguration config, CancellationToken cancellationToken = default)
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

    public async Task<AuthenticationConfigurationModel> UpdateAuthenticationConfiguration(string facilityId,
        AuthenticationConfiguration config, CancellationToken cancellationToken = default)
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
        var entity =
            await _queries.GetByFacilityIdAsync(facilityId, cancellationToken);

        if (entity == null)
            throw new NotFoundException();

        entity.Authentication = null;
        await _database.FhirListConfigurationRepository.SaveChangesAsync();
    }

    public async Task<FhirListConfigurationModel> AddAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default)
    {
        if (await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(l => l.FacilityId == entity.FacilityId) != null)
            throw new EntityAlreadyExistsException(
                $"A FhirListConfiguration already exists for facilityId: {entity.FacilityId}");

        var newEntity = await _database.FhirListConfigurationRepository.AddAsync(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return FhirListConfigurationModel.FromDomain(newEntity);
    }

    public async Task<FhirListConfigurationModel> UpdateAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity.FacilityId))
            throw new ArgumentNullException(nameof(entity.FacilityId));

        var existingEntity = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == entity.FacilityId);

        if (existingEntity == null)
            throw new MissingFacilityConfigurationException();

        existingEntity.Authentication = entity.Authentication;
        existingEntity.EHRPatientLists = entity.EHRPatientLists;
        existingEntity.FacilityId = entity.FacilityId;
        existingEntity.FhirBaseServerUrl = entity.FhirBaseServerUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return FhirListConfigurationModel.FromDomain(existingEntity);
    }

    public async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException();

        _database.FhirListConfigurationRepository.Remove(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return true;
    }
}