using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryListConfigurationManager
{
    Task<AuthenticationConfiguration?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirListConfiguration> AddAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default);
    Task<FhirListConfiguration> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<FhirListConfiguration?> SingleOrDefaultAsync(Expression<Func<FhirListConfiguration, bool>> predicate, CancellationToken cancellationToken = default);
    Task<FhirListConfiguration> UpdateAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryListConfigurationManager : IFhirQueryListConfigurationManager
{
    private readonly ILogger<FhirQueryListConfigurationManager> _logger;
    private readonly IDatabase _database;

    public FhirQueryListConfigurationManager(ILogger<FhirQueryListConfigurationManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database;
    }

    public async Task<AuthenticationConfiguration?> GetAuthenticationConfigurationByFacilityId(string facilityId,
        CancellationToken cancellationToken = default)
    {
        var queryResult =
            await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
        {
            throw new MissingFacilityConfigurationException(
                $"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        if (queryResult.Authentication == null)
        {
            throw new NotFoundException(
                $"No Authentication found on configuration for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return queryResult.Authentication;
    }

    public async Task<AuthenticationConfiguration> CreateAuthenticationConfiguration(string facilityId,
        AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult =
            await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

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

        return queryResult.Authentication;
    }

    public async Task<AuthenticationConfiguration> UpdateAuthenticationConfiguration(string facilityId,
        AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult =
            await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

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

        return queryResult.Authentication;
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity =
            await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException();

        entity.Authentication = null;
        await _database.FhirListConfigurationRepository.SaveChangesAsync();
    }

    public async Task<FhirListConfiguration> AddAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default)
    {
        if (await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(l => l.FacilityId == entity.FacilityId) != null)
            throw new EntityAlreadyExistsException(
                $"A FhirListConfiguration already exists for facilityId: {entity.FacilityId}");

        var newEntity = await _database.FhirListConfigurationRepository.AddAsync(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return newEntity;
    }

    public async Task<FhirListConfiguration> UpdateAsync(FhirListConfiguration entity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entity.FacilityId))
            throw new ArgumentNullException(nameof(entity.FacilityId));

        var existingEntity = await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == entity.FacilityId);

        if (existingEntity == null)
            throw new MissingFacilityConfigurationException();

        existingEntity.Authentication = entity.Authentication;
        existingEntity.EHRPatientLists = entity.EHRPatientLists;
        existingEntity.FacilityId = entity.FacilityId;
        existingEntity.FhirBaseServerUrl = entity.FhirBaseServerUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        await _database.FhirListConfigurationRepository.SaveChangesAsync();

        return existingEntity;
    }

    public async Task<bool> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var entity =
            await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        if (entity == null)
            throw new NotFoundException();

        _database.FhirListConfigurationRepository.Remove(entity);
        await _database.FhirListConfigurationRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FhirListConfiguration?> SingleOrDefaultAsync(Expression<Func<FhirListConfiguration, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(predicate);
    }

    public async Task<FhirListConfiguration> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _database.FhirListConfigurationRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);
    }
}
