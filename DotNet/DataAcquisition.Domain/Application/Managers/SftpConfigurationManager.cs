using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

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
    /// <exception cref="EntityAlreadyExistsException">Thrown if a configuration already exists for the organization.</exception>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null or empty.</exception>
    Task<SftpConfigurationModel> CreateAsync(SftpConfigurationModel model, string organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing SFTP configuration.
    /// </summary>
    /// <param name="model">The updated SFTP configuration model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated <see cref="SftpConfigurationModel"/>.</returns>
    /// <exception cref="NotFoundException">Thrown if the configuration does not exist.</exception>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null or empty.</exception>
    Task<SftpConfigurationModel> UpdateAsync(SftpConfigurationModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the SFTP configuration for the specified organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier for the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if the configuration was deleted; otherwise, <c>false</c>.</returns>
    /// <exception cref="NotFoundException">Thrown if the configuration does not exist.</exception>
    Task<bool> DeleteAsync(string organizationId, CancellationToken cancellationToken);
}


public class SftpConfigurationManager(IDatabase database) : ISftpConfigurationManager
{
    /// <inheritdoc/>
    public async Task<SftpConfigurationModel> CreateAsync(SftpConfigurationModel model, string organizationId, CancellationToken cancellationToken = default)
    {
        var existingEntity = await database.SftpConfigurationRepository
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (existingEntity is not null)
        {
            throw new EntityAlreadyExistsException(
                $"A {nameof(SftpConfiguration)} already exists for organizationId: {organizationId}");
        }

        if (string.IsNullOrEmpty(organizationId))
        {
            throw new ArgumentNullException(nameof(organizationId), "OrganizationId cannot be null or empty");
        }

        if (string.IsNullOrEmpty(model.Host))
        {
            throw new ArgumentNullException(nameof(model.Host), "Host cannot be null or empty");
        }

        // Default AuthType to Basic as per requirements
        model.AuthenticationProtocol = AuthType.Basic;

        var entity = model.ToEntity(organizationId);

        await database.SftpConfigurationRepository.AddAsync(entity, cancellationToken);
        await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

        return entity.ToModel();
    }

    /// <inheritdoc/>
    public async Task<SftpConfigurationModel> UpdateAsync(SftpConfigurationModel model, CancellationToken cancellationToken = default)
    {
        var existingEntity = await database.SftpConfigurationRepository
            .SingleOrDefaultAsync(q => q.Id == model.Id, cancellationToken);

        if (existingEntity is null)
            throw new NotFoundException($"No configuration found for Id: {model.Id}. Unable to update configuration.");

        if (string.IsNullOrEmpty(model.Host))
        {
            throw new ArgumentNullException(nameof(model.Host), "Host cannot be null or empty");
        }

        // Default AuthType to Basic as per requirements
        model.AuthenticationProtocol = AuthType.Basic;

        existingEntity.Host = model.Host;
        existingEntity.Port = model.Port;
        existingEntity.RemoteDirectory = model.RemoteDirectory;
        existingEntity.Timeout = model.Timeout;
        existingEntity.RemoveAfterProcessing = model.RemoveAfterProcessing;
        existingEntity.AuthenticationProtocol = model.AuthenticationProtocol;

        await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

        return existingEntity.ToModel();
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var entity = await database.SftpConfigurationRepository
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (entity is null)
            throw new NotFoundException($"No configuration found for organizationId: {organizationId}. Unable to delete configuration.");

        database.SftpConfigurationRepository.Remove(entity);
        await database.SftpConfigurationRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
