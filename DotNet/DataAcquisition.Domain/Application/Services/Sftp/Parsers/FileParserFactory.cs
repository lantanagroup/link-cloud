using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;

/// <summary>
/// Factory for selecting the appropriate <see cref="IFileParser{TResult}"/> based on file characteristics.
/// Implementations resolve parsers from the dependency injection container and select the one
/// capable of handling the requested file type and acquisition context.
/// </summary>
public interface IFileParserFactory
{
    /// <summary>
    /// Gets a parser capable of handling files with the specified characteristics.
    /// </summary>
    /// <typeparam name="TResult">The type of records the parser should produce.</typeparam>
    /// <param name="acquisitionType">The type of SFTP acquisition being processed.</param>
    /// <param name="fileExtension">The file extension including the dot (e.g., ".csv", ".txt").</param>
    /// <param name="config">Optional parsing configuration that may affect parser selection.</param>
    /// <returns>An <see cref="IFileParser{TResult}"/> that can parse the file.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when no parser is registered for the combination of acquisition type, file extension, and result type.
    /// </exception>
    IFileParser<TResult> GetParser<TResult>(
        SftpAcquisitionType acquisitionType,
        string fileExtension,
        FileParsingConfiguration? config);
}

/// <summary>
/// Resolves file parsers from the DI container based on file characteristics.
/// </summary>
public class FileParserFactory : IFileParserFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileParserFactory> _logger;

    public FileParserFactory(IServiceProvider serviceProvider, ILogger<FileParserFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
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
