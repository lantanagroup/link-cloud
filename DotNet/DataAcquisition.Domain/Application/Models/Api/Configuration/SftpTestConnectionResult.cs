using System.ComponentModel;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

[Description("Result model for ad-hoc SFTP connection test operations.")]
public record SftpTestConnectionResult
{
    /// <summary>
    /// Indicates whether the SFTP connection test was successful.
    /// </summary>
    [Description("Indicates whether the connection test was successful.")]
    public bool Success { get; set; }

    /// <summary>
    /// A message describing the result of the connection test.
    /// </summary>
    [Description("A message describing the result of the connection test.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Files found in reportDirectory. Only populated when includeFileContent=true is passed.
    /// </summary>
    [Description("Files found in reportDirectory. Only populated when includeFileContent=true is passed.")]
    public SftpTestFileModel[]? Files { get; set; } = null;

}