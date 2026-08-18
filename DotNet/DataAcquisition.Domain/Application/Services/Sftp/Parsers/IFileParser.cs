using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;

/// <summary>
/// Defines operations for parsing files retrieved from SFTP servers.
/// Implementations handle specific file formats and acquisition types,
/// yielding strongly-typed records as they are parsed.
/// </summary>
/// <typeparam name="TResult">The type of records produced by parsing the file.</typeparam>
public interface IFileParser<TResult>
{
    /// <summary>
    /// Determines if this parser can handle a file with the given characteristics.
    /// </summary>
    /// <param name="acquisitionType">The type of SFTP acquisition being processed.</param>
    /// <param name="subType">The subtype of SFTP acquisition being processed.</param>
    /// <param name="fileExtension">The file extension including the dot (e.g., ".csv", ".txt").</param>
    /// <param name="config">Optional parsing configuration that may affect parser selection.</param>
    /// <returns><c>true</c> if this parser can handle the file; otherwise, <c>false</c>.</returns>
    bool CanParse(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType, string fileExtension, FileParsingConfiguration? config);

    /// <summary>
    /// Parses the file stream and yields records as they are parsed.
    /// Uses async streaming to support large files without loading everything into memory.
    /// </summary>
    /// <param name="fileStream">The stream containing the file contents to parse.</param>
    /// <param name="config">Optional parsing configuration (e.g., delimiter, encoding, header mapping).</param>
    /// <param name="cancellationToken">Token to cancel the parsing operation.</param>
    /// <returns>An async enumerable of parsed records.</returns>
    IAsyncEnumerable<TResult> ParseAsync(
        Stream fileStream,
        FileParsingConfiguration? config,
        CancellationToken cancellationToken);
}
