using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Factory for selecting the appropriate SFTP acquisition processor based on type.
/// </summary>
public interface ISftpAcquisitionProcessorFactory
{
    /// <summary>
    /// Gets a processor that can handle the given acquisition type.
    /// </summary>
    ISftpAcquisitionProcessor GetProcessor(SftpAcquisitionType acquisitionType);
}
