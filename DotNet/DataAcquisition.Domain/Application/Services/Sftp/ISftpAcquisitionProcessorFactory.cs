using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Factory for selecting the appropriate <see cref="ISftpAcquisitionProcessor"/> based on acquisition type.
/// Implementations resolve processors from the dependency injection container and select the one
/// capable of handling the requested <see cref="SftpAcquisitionType"/>.
/// </summary>
public interface ISftpAcquisitionProcessorFactory
{
    /// <summary>
    /// Gets a processor capable of handling the specified acquisition type.
    /// </summary>
    /// <param name="acquisitionType">The type of SFTP acquisition to process.</param>
    /// <returns>An <see cref="ISftpAcquisitionProcessor"/> that can handle the acquisition type.</returns>
    /// <exception cref="NotSupportedException">Thrown when no processor is registered for the acquisition type.</exception>
    ISftpAcquisitionProcessor GetProcessor(SftpAcquisitionType acquisitionType);
}
