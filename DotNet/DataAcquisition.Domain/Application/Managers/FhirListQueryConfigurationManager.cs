using System.Diagnostics;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
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
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);

        if (await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(l => l.FacilityId == model.FacilityId,
                cancellationToken) != null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FhirListConfiguration already exists");
            throw new EntityAlreadyExistsException(
                $"A FhirListConfiguration already exists for facilityId: {model.FacilityId?.SanitizeAndRemove()}");
        }

        if(string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be null");
            throw new ArgumentNullException(nameof(model.FacilityId));
        }

        if (string.IsNullOrEmpty(model.FhirBaseServerUrl))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FhirBaseServerUrl cannot be null");
            throw new ArgumentNullException(nameof(model.FhirBaseServerUrl));
        }

        var entity = new FhirListConfiguration()
        {
            EHRPatientLists = model.EHRPatientLists.Select(e => new EhrPatientList
            {
                FhirId = e.FhirId,
                InternalId = e.InternalId,
                Status = e.Status ?? throw new ArgumentNullException(nameof(e.Status)),
                TimeFrame = e.TimeFrame ?? throw new ArgumentNullException(nameof(e.TimeFrame)),
            }).ToList(),
            FhirBaseServerUrl = model.FhirBaseServerUrl,
            FacilityId = model.FacilityId,
            Authentication = model.Authentication?.ToDomain(),
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
        };
        
        var newEntity = await _database.FhirListConfigurationRepository.AddAsync(entity, cancellationToken);
        await _database.FhirListConfigurationRepository.SaveChangesAsync(cancellationToken);

        return FhirListConfigurationModel.FromDomain(newEntity);
    }

    public async Task<FhirListConfigurationModel> UpdateAsync(UpdateFhirListConfigurationModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        
        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be null");
            throw new ArgumentNullException(model.FacilityId);
        }

        if (string.IsNullOrEmpty(model.FhirBaseServerUrl))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FhirBaseServerUrl cannot be null");
            throw new ArgumentNullException(model.FhirBaseServerUrl);
        }

        var existingEntity = await _database.FhirListConfigurationRepository.SingleOrDefaultAsync(q => q.FacilityId == model.FacilityId, cancellationToken);

        if (existingEntity is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "No FHIR facility list configuration found for facilityId");
            throw new MissingFacilityConfigurationException();
        }

        existingEntity.Authentication = model.Authentication?.ToDomain();
        existingEntity.EHRPatientLists = model.EHRPatientLists.Select(e => new EhrPatientList
        {
            FhirId = e.FhirId,
            InternalId = e.InternalId,
            Status = e.Status ?? throw new ArgumentNullException(nameof(e.Status)),
            TimeFrame = e.TimeFrame ?? throw new ArgumentNullException(nameof(e.TimeFrame)),
        }).ToList();
        existingEntity.FacilityId = model.FacilityId;
        existingEntity.FhirBaseServerUrl = model.FhirBaseServerUrl;
        existingEntity.ModifyDate = DateTime.UtcNow;

        await _database.FhirListConfigurationRepository.SaveChangesAsync(cancellationToken);

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