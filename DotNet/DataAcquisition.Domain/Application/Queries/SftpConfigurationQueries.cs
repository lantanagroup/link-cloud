using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

/// <summary>
/// Provides query operations for the SFTP configuration entity.
/// </summary>
public interface ISftpConfigurationQueries
{
    /// <summary>
    /// Retrieves the SFTP configuration by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the SFTP configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The <see cref="SftpConfigurationModel"/> if found; otherwise, <c>null</c>.</returns>
    Task<SftpConfigurationModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the SFTP configuration for a specific organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The <see cref="SftpConfigurationModel"/> if found; otherwise, <c>null</c>.</returns>
    Task<SftpConfigurationModel?> GetByOrganizationIdAsync(string organizationId, CancellationToken cancellationToken);
}


public class SftpConfigurationQueries(DataAcquisitionDbContext database) : ISftpConfigurationQueries
{
    /// <inheritdoc/>
    public async Task<SftpConfigurationModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await (from config in database.SftpConfigurations
                            where config.Id == id
                            select config.ToModel()).SingleOrDefaultAsync(cancellationToken);

        return result;
    }

    /// <inheritdoc/>
    public async Task<SftpConfigurationModel?> GetByOrganizationIdAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var result = await (from config in database.SftpConfigurations
                            where config.OrganizationId == organizationId
                            select config.ToModel()).SingleOrDefaultAsync(cancellationToken);

        return result;
    }
}
