using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
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

        RuleForEach(x => x.AcquisitionConfigurations)
            .Must((model, config) => SftpConfigurationValidationRules.HandlesProcessedAdtFiles(config, model.RemoveAfterProcessing))
            .WithMessage(SftpConfigurationValidationRules.AdtFilesMustBeHandledMessage);

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

        RuleForEach(x => x.AcquisitionConfigurations)
            .Must((model, config) => SftpConfigurationValidationRules.HandlesProcessedAdtFiles(config, model.RemoveAfterProcessing))
            .WithMessage(SftpConfigurationValidationRules.AdtFilesMustBeHandledMessage);

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

        RuleFor(x => x.AcquisitionType)
            .Equal(SftpAcquisitionType.Census)
            .WithMessage("The HL7ADT sub type is only supported for Census acquisition.")
            .When(x => x.SubType == SftpAcquisitionSubType.HL7ADT);

        RuleFor(x => x.AdtConfiguration)
            .NotNull()
            .WithMessage("An ADT configuration is required for the HL7ADT sub type.")
            .SetValidator(new AdtConfigurationValidator()!)
            .When(x => x.SubType == SftpAcquisitionSubType.HL7ADT);

        RuleFor(x => x.AdtConfiguration)
            .Null()
            .WithMessage("An ADT configuration is only allowed for the HL7ADT sub type.")
            .When(x => x.SubType != SftpAcquisitionSubType.HL7ADT);
    }
}

/// <summary>
/// Validator for AdtConfiguration (nested within an SFTP acquisition configuration)
/// </summary>
public class AdtConfigurationValidator : AbstractValidator<AdtConfiguration>
{
    private const int MaxLocationLength = 128;
    private const int MaxSystemLength = 512;

    public AdtConfigurationValidator()
    {
        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.PatientIdLocation) != string.IsNullOrWhiteSpace(x.MrnIdentifierSystem))
            .WithName(nameof(AdtConfiguration))
            .WithMessage("Provide exactly one of PatientIdLocation (the Patient.Id is on the ADT event) or MrnIdentifierSystem (the Patient.Id is found by MRN search).");

        RuleFor(x => x.PatientIdLocation).Must(BeValidLocation).WithMessage(InvalidLocationMessage("PatientIdLocation"));
        RuleFor(x => x.MrnLocation).Must(BeValidLocation).WithMessage(InvalidLocationMessage("MrnLocation"));
        RuleFor(x => x.EncounterIdLocation).Must(BeValidLocation).WithMessage(InvalidLocationMessage("EncounterIdLocation"));

        RuleFor(x => x.MrnIdentifierSystem)
            .Must(system => system!.Length <= MaxSystemLength && !system.Any(c => char.IsWhiteSpace(c) || c == '|'))
            .WithMessage($"MrnIdentifierSystem cannot contain whitespace or '|', or exceed {MaxSystemLength} characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.MrnIdentifierSystem));
    }

    private static bool BeValidLocation(string? location) =>
        string.IsNullOrWhiteSpace(location)
        || (location.Length <= MaxLocationLength && Hl7Path.TryParse(location, out _));

    private static string InvalidLocationMessage(string name) =>
        $"{name} must be an HL7 location such as PID-2, PID-3[5=MR].1 or OBX[3.1=FHIRID]-5.";
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

public class SftpTestConnectionRequestModelValidator : AbstractValidator<SftpTestConnectionRequestModel>
{
    private const int MaxUsernameLength = 256;
    private const int MaxPasswordLength = 1024;

    public SftpTestConnectionRequestModelValidator(IOptions<SftpValidationSettings> options)
    {
        var settings = options.Value.Connection;

        RuleFor(x => x.HostName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("HostName is required.")
            .MaximumLength(settings.MaxHostLength)
            .WithMessage($"HostName cannot exceed {settings.MaxHostLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidHostName)
            .WithMessage("HostName must be a valid hostname or IP address.");

        RuleFor(x => x.HostUrlPort)
            .InclusiveBetween(1, 65535)
            .WithMessage("HostUrlPort must be between 1 and 65535.");

        RuleFor(x => x.ReportDirectory)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ReportDirectory is required. Use '/' for the root directory.")
            .MaximumLength(settings.MaxRemoteDirectoryLength)
            .WithMessage($"ReportDirectory path cannot exceed {settings.MaxRemoteDirectoryLength} characters.")
            .Must(SftpConfigurationValidationRules.BeValidRemoteDirectoryPath)
            .WithMessage("ReportDirectory path contains invalid characters.");

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
    public const string AdtFilesMustBeHandledMessage =
        "HL7ADT acquisition requires a processed directory or RemoveAfterProcessing, so that a file of ADT events is not read again on every run.";

    /// <summary>
    /// Validates that an ADT feed's files leave the directory once read
    /// </summary>
    public static bool HandlesProcessedAdtFiles(SftpAcquisitionTypeConfiguration config, bool removeAfterProcessing) =>
        config.SubType != SftpAcquisitionSubType.HL7ADT
        || removeAfterProcessing
        || !string.IsNullOrWhiteSpace(config.ProcessedDirectory);

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
