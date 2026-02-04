using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

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
    public ISftpAcquisitionProcessor GetProcessor(SftpAcquisitionType acquisitionType)
    {
        var processors = _serviceProvider.GetServices<ISftpAcquisitionProcessor>();
        var processor = processors.FirstOrDefault(p => p.CanProcess(acquisitionType));

        if (processor is null)
        {
            _logger.LogError("No processor found for AcquisitionType={Type}", acquisitionType);
            throw new NotSupportedException($"No processor available for {acquisitionType}");
        }

        _logger.LogDebug("Selected processor {ProcessorType} for {AcquisitionType}",
            processor.GetType().Name, acquisitionType);
        return processor;
    }
}
