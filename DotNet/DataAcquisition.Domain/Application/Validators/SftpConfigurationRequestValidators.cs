using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

/// <summary>
/// Validator for CreateSftpConfigurationModel
/// </summary>
public class CreateSftpConfigurationModelValidator : AbstractValidator<CreateSftpConfigurationModel>
{
    public CreateSftpConfigurationModelValidator(IOptions<SftpValidationSettings> options)
    {
        var settings = options.Value.Connection;

        RuleFor(x => x.Host)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Host is required.")
            .MaximumLength(settings.MaxHostLength)
            .WithMessage($"Host cannot exceed {settings.MaxHostLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidHostName)
            .WithMessage("Host must be a valid hostname or IP address.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be between 1 and 65535.");

        RuleFor(x => x.RemoteDirectory)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Remote directory is required. Use '/' for the root directory.")
            .MaximumLength(settings.MaxRemoteDirectoryLength)
            .WithMessage($"Remote directory path cannot exceed {settings.MaxRemoteDirectoryLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidRemoteDirectoryPath)
            .WithMessage("Remote directory path contains invalid characters.");

        // Validate nested acquisition configurations
        RuleForEach(x => x.AcquisitionConfigurations)
            .SetValidator(new SftpAcquisitionTypeConfigurationValidator(options));

        RuleFor(x => x.Timeout)
            .Must(timeout => timeout >= TimeSpan.Zero && timeout <= TimeSpan.FromMinutes(settings.MaxTimeoutMinutes))
            .WithMessage($"Timeout must be between 0 and {settings.MaxTimeoutMinutes} minutes.");

        // Validate nested credentials if provided
        RuleFor(x => x.Credentials)
            .SetValidator(new SftpCredentialsModelValidator()!)
            .When(x => x.Credentials is not null);
    }
}

/// <summary>
/// Validator for SftpConfigurationModel (used for updates)
/// </summary>
public class SftpConfigurationModelValidator : AbstractValidator<SftpConfigurationModel>
{
    public SftpConfigurationModelValidator(IOptions<SftpValidationSettings> options)
    {
        var settings = options.Value.Connection;

        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Configuration Id is required.");

        RuleFor(x => x.Host)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Host is required.")
            .MaximumLength(settings.MaxHostLength)
            .WithMessage($"Host cannot exceed {settings.MaxHostLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidHostName)
            .WithMessage("Host must be a valid hostname or IP address.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("Port must be between 1 and 65535.");

        RuleFor(x => x.RemoteDirectory)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Remote directory is required. Use '/' for the root directory.")
            .MaximumLength(settings.MaxRemoteDirectoryLength)
            .WithMessage($"Remote directory path cannot exceed {settings.MaxRemoteDirectoryLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidRemoteDirectoryPath)
            .WithMessage("Remote directory path contains invalid characters.");

        // Validate nested acquisition configurations
        RuleForEach(x => x.AcquisitionConfigurations)
            .SetValidator(new SftpAcquisitionTypeConfigurationValidator(options));

        RuleFor(x => x.Timeout)
            .Must(timeout => timeout >= TimeSpan.Zero && timeout <= TimeSpan.FromMinutes(settings.MaxTimeoutMinutes))
            .WithMessage($"Timeout must be between 0 and {settings.MaxTimeoutMinutes} minutes.");
    }
}

/// <summary>
/// Validator for SftpAcquisitionTypeConfiguration (nested within SFTP configuration)
/// </summary>
public class SftpAcquisitionTypeConfigurationValidator : AbstractValidator<SftpAcquisitionTypeConfiguration>
{
    public SftpAcquisitionTypeConfigurationValidator(IOptions<SftpValidationSettings> options)
    {
        var settings = options.Value.Connection;

        RuleFor(x => x.RemoteDirectory)
            .Cascade(CascadeMode.Stop)
            .Must(path => !string.IsNullOrWhiteSpace(path))
            .WithMessage("Acquisition remote directory cannot be empty. Use '/' for the root directory or leave the field blank to inherit from the connection-level directory.")
            .MaximumLength(settings.MaxRemoteDirectoryLength)
            .WithMessage($"Acquisition remote directory path cannot exceed {settings.MaxRemoteDirectoryLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidRemoteDirectoryPath)
            .WithMessage("Acquisition remote directory path contains invalid characters.")
            .When(x => x.RemoteDirectory is not null);

        RuleFor(x => x.ProcessedDirectory)
            .Cascade(CascadeMode.Stop)
            .Must(path => !string.IsNullOrWhiteSpace(path))
            .WithMessage("Processed directory cannot be empty. Provide a valid path or leave the field blank.")
            .MaximumLength(settings.MaxRemoteDirectoryLength)
            .WithMessage($"Processed directory path cannot exceed {settings.MaxRemoteDirectoryLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidRemoteDirectoryPath)
            .WithMessage("Processed directory path contains invalid characters.")
            .When(x => x.ProcessedDirectory is not null);
    }
}

/// <summary>
/// Validator for SftpCredentialsModel
/// </summary>
public class SftpCredentialsModelValidator : AbstractValidator<SftpCredentialsModel>
{
    private const int MaxUsernameLength = 256;
    private const int MaxPasswordLength = 1024;

    public SftpCredentialsModelValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required when providing credentials.")
            .MaximumLength(MaxUsernameLength)
            .WithMessage($"Username cannot exceed {MaxUsernameLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required when providing credentials.")
            .MaximumLength(MaxPasswordLength)
            .WithMessage($"Password cannot exceed {MaxPasswordLength} characters.");
    }
}

/// <summary>
/// Shared validation rules for SFTP configuration
/// </summary>
public static class SftpConfigurationValidationRules
{
    /// <summary>
    /// Characters that are not allowed in remote directory paths
    /// </summary>
    private static readonly char[] InvalidPathChars = ['<', '>', '"', '|', '?', '*', '\0'];

    /// <summary>
    /// Validates that the host is a valid hostname or IP address
    /// </summary>
    public static bool BeValidHostName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        // Check for invalid characters in hostname
        if (host.Any(c => char.IsWhiteSpace(c) || c == '/' || c == '\\'))
            return false;

        // Basic validation - hostname should not start/end with dots or hyphens
        if (host.StartsWith('.') || host.StartsWith('-') || host.EndsWith('.') || host.EndsWith('-'))
            return false;

        return true;
    }

    /// <summary>
    /// Validates that the remote directory path does not contain invalid characters
    /// </summary>
    public static bool BeValidRemoteDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return !path.Any(c => InvalidPathChars.Contains(c));
    }
}
