using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

/// <summary>
/// Validator for CreateSftpLogRequest
/// </summary>
public class CreateSftpLogRequestValidator : AbstractValidator<CreateSftpLogRequest>
{
    public CreateSftpLogRequestValidator(IOptions<SftpValidationSettings> options)
    {
        var settings = options.Value.FileName;
        var allowedExtensions = settings.GetEffectiveAllowedExtensions();

        RuleFor(x => x.FacilityId)
            .NotEmpty()
            .WithMessage("Facility ID is required.");

        RuleFor(x => x.AcquisitionType)
            .IsInEnum()
            .WithMessage("Type must be a valid SftpAcquisitionType.");

        RuleFor(x => x.SubType)
            .IsInEnum()
            .WithMessage("SubType must be a valid SftpAcquisitionSubType.");
    }
}

/// <summary>
/// Shared validation rules for SFTP file names
/// </summary>
public static class SftpFileNameValidationRules
{
    /// <summary>
    /// Characters that are not allowed in file names
    /// </summary>
    private static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '|', '?', '*', '\0', '/', '\\'];

    /// <summary>
    /// Validates that the file name has an allowed extension
    /// </summary>
    /// <param name="fileName">The file name to validate</param>
    /// <param name="allowedExtensions">The list of allowed extensions</param>
    /// <returns>True if the extension is valid, false otherwise</returns>
    public static bool HaveValidExtension(string? fileName, string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension))
            return false;

        return allowedExtensions.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// Validates that the file name does not contain invalid characters
    /// </summary>
    public static bool NotContainInvalidCharacters(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return !fileName.Any(c => InvalidFileNameChars.Contains(c));
    }
}
