using System.ComponentModel.DataAnnotations;
using System.Text;
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
    [Required(ErrorMessage = "Host name is required.")]
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// The SFTP host port to connect to. Defaults to 22 if not specified.
    /// </summary>
    [JsonPropertyName("hostUrlPort")]
    [Required(ErrorMessage = "Host port is required.")]
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

    /// <summary>
    /// Leaves <see cref="Password"/> out of the compiler-generated <c>ToString</c>, which otherwise prints every
    /// public property. A property added to this record is not printed until it is added here too.
    /// </summary>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(HostName)} = {HostName}, ");
        builder.Append($"{nameof(HostUrlPort)} = {HostUrlPort}, ");
        builder.Append($"{nameof(Username)} = {Username}, ");
        builder.Append($"{nameof(ReportDirectory)} = {ReportDirectory}");
        return true;
    }
}