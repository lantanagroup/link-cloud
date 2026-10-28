using System.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

/// <summary>
/// Manages SFTP configuration operations for an organization.
/// </summary>
public interface ISftpConfigurationManager
{
    /// <summary>
    /// Creates a new SFTP configuration for the specified organization.
    /// </summary>
    /// <param name="model">The SFTP configuration model to create.</param>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created <see cref="SftpConfigurationModel"/>.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an SFTP configuration already exists for the organization.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the organization already has a FHIR List configuration (only one census acquisition method allowed).</exception>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null or empty.</exception>
    Task<SftpConfigurationModel> CreateAsync(SftpConfigurationModel model, string organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing SFTP configuration.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="model">The updated SFTP configuration model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated <see cref="SftpConfigurationModel"/>.</returns>
    /// <exception cref="NotFoundException">Thrown if the configuration does not exist.</exception>
    /// <exception cref="OrganizationalAccessException">Thrown if the configuration does not belong to the organization.</exception>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null or empty.</exception>
    Task<SftpConfigurationModel> UpdateAsync(string organizationId, SftpConfigurationModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the SFTP configuration for the specified organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="configurationId">The unique identifier for the configuration</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if the configuration was deleted; otherwise, <c>false</c>.</returns>
    /// <exception cref="NotFoundException">Thrown if the configuration does not exist.</exception>
    Task<bool> DeleteAsync(string organizationId, Guid configurationId, CancellationToken cancellationToken);
}


public class SftpConfigurationManager(
    ILogger<SftpConfigurationManager> logger,
    IDatabase database,
    IFhirQueryListConfigurationQueries fhirListConfigurationQueries) : ISftpConfigurationManager
{
    /// <inheritdoc/>
    public async Task<SftpConfigurationModel> CreateAsync(SftpConfigurationModel model, string organizationId, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, organizationId);

        if (string.IsNullOrEmpty(organizationId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "OrganizationId cannot be null or empty");
            throw new ArgumentNullException(nameof(organizationId), "OrganizationId cannot be null or empty");
        }

        if (string.IsNullOrEmpty(model.Host))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Host cannot be null or empty");
            throw new ArgumentNullException(nameof(model.Host), "Host cannot be null or empty");
        }

        // Verify the facility doesn't already have a FHIR List configuration
        // A facility can only have one census acquisition method (SFTP or FHIR List)
        var existingFhirListConfig = await fhirListConfigurationQueries.GetByFacilityIdAsync(organizationId, cancellationToken);
        if (existingFhirListConfig is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FHIR List configuration already exists for facility");
            throw new InvalidOperationException(
                $"The facility '{organizationId}' already has a FHIR List configuration. A facility can only have one census acquisition method.");
        }

        // Verify an SFTP configuration doesn't already exist for this organization
        var existingEntity = await database.SftpConfigurationRepository
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (existingEntity is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP configuration already exists for organization");
            throw new EntityAlreadyExistsException(
                $"A {nameof(SftpConfiguration)} already exists for organizationId: {organizationId}");
        }

        try
        {
            // Default AuthType to Basic as per requirements
            model.AuthenticationProtocol = AuthType.Basic;

            var entity = model.ToEntity(organizationId);

            await database.SftpConfigurationRepository.AddAsync(entity, cancellationToken);
            await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

            return entity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpConfiguration", System.Text.Json.JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            logger.LogError(ex, "Error creating SFTP Configuration for OrganizationId: {OrganizationId}", organizationId.Sanitize());
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SftpConfigurationModel> UpdateAsync(string organizationId, SftpConfigurationModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, organizationId);

        var existingEntity = await database.SftpConfigurationRepository
            .SingleOrDefaultAsync(q => q.Id == model.Id, cancellationToken);

        if (existingEntity is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "No configuration found to update");
            throw new NotFoundException($"No configuration found for Id: {model.Id}. Unable to update configuration.");
        }

        if (existingEntity.OrganizationId != organizationId)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Organizational access violation");
            throw new OrganizationalAccessException(existingEntity.OrganizationId, organizationId);
        }

        if (string.IsNullOrEmpty(model.Host))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Host cannot be null or empty");
            throw new ArgumentNullException(nameof(model.Host), "Host cannot be null or empty");
        }

        try
        {
            // Default AuthType to Basic as per requirements
            model.AuthenticationProtocol = AuthType.Basic;

            existingEntity.Host = model.Host;
            existingEntity.Port = model.Port;
            existingEntity.RemoteDirectory = model.RemoteDirectory;
            existingEntity.Timeout = model.Timeout;
            existingEntity.RemoveAfterProcessing = model.RemoveAfterProcessing;
            existingEntity.EnableBenchmarking = model.EnableBenchmarking;
            existingEntity.AcquisitionConfigurations = model.AcquisitionConfigurations;

            // For now, we are only allowing Basic auth, so no need to update this field
            //existingEntity.AuthenticationProtocol = model.AuthenticationProtocol;

            await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

            return existingEntity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpConfiguration", System.Text.Json.JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            logger.LogError(ex, "Error updating SFTP Configuration for OrganizationId: {OrganizationId}, ConfigurationId: {ConfigurationId}", organizationId.Sanitize(), model.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string organizationId, Guid configurationId, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, organizationId);

        var entity = await database.SftpConfigurationRepository
            .FirstOrDefaultAsync(x => x.Id == configurationId, cancellationToken);

        if (entity is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "No configuration found to delete");
            throw new NotFoundException($"No configuration found for Id: {configurationId}. Unable to delete configuration.");
        }

        if (entity.OrganizationId != organizationId)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Organizational access violation");
            throw new OrganizationalAccessException(entity.OrganizationId, organizationId);
        }

        try
        {
            database.SftpConfigurationRepository.Remove(entity);
            await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error deleting SFTP Configuration for OrganizationId: {OrganizationId}, ConfigurationId: {ConfigurationId}", organizationId.Sanitize(), configurationId);
            throw;
        }
    }
}
