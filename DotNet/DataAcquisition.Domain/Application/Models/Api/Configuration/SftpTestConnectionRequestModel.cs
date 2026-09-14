using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// Represents the request model for testing SFTP connection settings.
/// </summary>
public record SftpTestConnectionRequestModel
{
    /// <summary>
    /// The SFTP host name to connect to.
    /// </summary>
    [JsonPropertyName("hostName")]
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// The SFTP host port to connect to. Defaults to 22 if not specified.
    /// </summary>
    [JsonPropertyName("hostUrlPort")]
    public int HostUrlPort { get; set; } = 22;

    /// <summary>
    /// Username for SFTP authentication.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for the SFTP user.
    /// </summary>
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The directory where reports will be stored.
    /// </summary>
    [JsonPropertyName("reportDirectory")]
    public string ReportDirectory { get; set; } = "/";
}