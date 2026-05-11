using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Factory for selecting the appropriate <see cref="ISftpAcquisitionProcessor"/> based on acquisition type and subtype.
/// Implementations resolve processors from the dependency injection container and select the one
/// capable of handling the requested <see cref="SftpAcquisitionType"/> and <see cref="SftpAcquisitionSubType"/>.
/// </summary>
public interface ISftpAcquisitionProcessorFactory
{
    /// <summary>
    /// Gets a processor capable of handling the specified acquisition type and subtype.
    /// </summary>
    /// <param name="acquisitionType">The type of SFTP acquisition to process.</param>
    /// <param name="subType">The subtype of SFTP acquisition to process.</param>
    /// <returns>An <see cref="ISftpAcquisitionProcessor"/> that can handle the acquisition type and subtype.</returns>
    /// <exception cref="NotSupportedException">Thrown when no processor is registered for the acquisition type and subtype.</exception>
    ISftpAcquisitionProcessor GetProcessor(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType);
}


/// <summary>
/// Resolves SFTP acquisition processors from the DI container.
/// </summary>
public class SftpAcquisitionProcessorFactory : ISftpAcquisitionProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SftpAcquisitionProcessorFactory> _logger;

    public SftpAcquisitionProcessorFactory(
        IServiceProvider serviceProvider,
        ILogger<SftpAcquisitionProcessorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ISftpAcquisitionProcessor GetProcessor(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType)
    {
        var processors = _serviceProvider.GetServices<ISftpAcquisitionProcessor>();
        var processor = processors.FirstOrDefault(p => p.CanProcess(acquisitionType, subType));

        if (processor is null)
        {
            _logger.LogError("No processor found for AcquisitionType={Type}, SubType={SubType}", acquisitionType, subType);
            throw new NotSupportedException($"No processor available for {acquisitionType}/{subType}");
        }

        _logger.LogDebug("Selected processor {ProcessorType} for {AcquisitionType}/{SubType}",
            processor.GetType().Name, acquisitionType, subType);
        return processor;
    }
}
