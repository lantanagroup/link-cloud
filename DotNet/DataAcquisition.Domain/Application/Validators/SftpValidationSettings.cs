namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

/// <summary>
/// Combined configuration settings for all SFTP-related validation
/// </summary>
public class SftpValidationSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "SftpValidation";

    /// <summary>
    /// File name validation settings
    /// </summary>
    public SftpFileNameSettings FileName { get; set; } = new();

    /// <summary>
    /// Connection/configuration validation settings
    /// </summary>
    public SftpConnectionSettings Connection { get; set; } = new();
}

/// <summary>
/// Settings for SFTP file name validation
/// </summary>
public class SftpFileNameSettings
{
    /// <summary>
    /// Default allowed file extensions when none are configured
    /// </summary>
    public static readonly string[] DefaultAllowedExtensions = [".csv", ".json", ".xml", ".dat"];

    /// <summary>
    /// Default maximum file name length
    /// </summary>
    public const int DefaultMaxLength = 255;

    /// <summary>
    /// List of allowed file extensions (e.g., ".csv", ".json", ".xml").
    /// If not configured or empty, defaults to DefaultAllowedExtensions.
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>
    /// Maximum allowed length for file names.
    /// </summary>
    public int MaxLength { get; set; } = DefaultMaxLength;

    /// <summary>
    /// Gets the effective list of allowed extensions, using defaults if none configured
    /// </summary>
    public string[] GetEffectiveAllowedExtensions()
    {
        return AllowedExtensions is { Length: > 0 }
            ? AllowedExtensions
            : DefaultAllowedExtensions;
    }
}

/// <summary>
/// Settings for SFTP connection/configuration validation
/// </summary>
public class SftpConnectionSettings
{
    /// <summary>
    /// Default maximum host length
    /// </summary>
    public const int DefaultMaxHostLength = 256;

    /// <summary>
    /// Default maximum remote directory path length
    /// </summary>
    public const int DefaultMaxRemoteDirectoryLength = 4096;

    /// <summary>
    /// Default maximum timeout in minutes
    /// </summary>
    public const int DefaultMaxTimeoutMinutes = 30;

    /// <summary>
    /// Maximum allowed length for the host field.
    /// </summary>
    public int MaxHostLength { get; set; } = DefaultMaxHostLength;

    /// <summary>
    /// Maximum allowed length for the remote directory path.
    /// </summary>
    public int MaxRemoteDirectoryLength { get; set; } = DefaultMaxRemoteDirectoryLength;

    /// <summary>
    /// Maximum allowed timeout in minutes.
    /// </summary>
    public int MaxTimeoutMinutes { get; set; } = DefaultMaxTimeoutMinutes;
}
