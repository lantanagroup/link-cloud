using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Factory for selecting the appropriate file parser.
/// </summary>
public interface IFileParserFactory
{
    /// <summary>
    /// Gets a parser that returns the specified result type.
    /// </summary>
    IFileParser<TResult> GetParser<TResult>(
        SftpAcquisitionType acquisitionType,
        string fileExtension,
        FileParsingConfiguration? config);
}
