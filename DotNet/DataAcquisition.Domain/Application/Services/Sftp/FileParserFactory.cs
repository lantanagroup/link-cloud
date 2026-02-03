using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

public class FileParserFactory : IFileParserFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileParserFactory> _logger;

    public FileParserFactory(IServiceProvider serviceProvider, ILogger<FileParserFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IFileParser<TResult> GetParser<TResult>(
        SftpAcquisitionType acquisitionType,
        string fileExtension,
        FileParsingConfiguration? config)
    {
        // Get all registered parsers of the requested result type
        var parsers = _serviceProvider.GetServices<IFileParser<TResult>>();

        var parser = parsers.FirstOrDefault(p => p.CanParse(acquisitionType, fileExtension, config));

        if (parser is null)
        {
            _logger.LogError(
                "No parser found for AcquisitionType={Type}, FileExtension={FileExtension}, ResultType={ResultType}",
                acquisitionType, fileExtension, typeof(TResult).Name);
            throw new NotSupportedException(
                $"No parser available for {acquisitionType}/{fileExtension} returning {typeof(TResult).Name}");
        }

        _logger.LogDebug("Selected parser {ParserType} for file extension {FileExtension}", parser.GetType().Name, fileExtension);
        return parser;
    }
}
