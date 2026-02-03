using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Generic interface for parsing SFTP files.
/// Different implementations return different result types based on use case.
/// </summary>
/// <typeparam name="TResult">The type of parsed records.</typeparam>
public interface IFileParser<TResult>
{
    /// <summary>
    /// Determines if this parser can handle the given file.
    /// </summary>
    bool CanParse(SftpAcquisitionType acquisitionType, string fileExtension, FileParsingConfiguration? config);

    /// <summary>
    /// Parses the file stream and yields records of type TResult.
    /// </summary>
    IAsyncEnumerable<TResult> ParseAsync(
        Stream fileStream,
        FileParsingConfiguration? config,
        CancellationToken cancellationToken);
}
