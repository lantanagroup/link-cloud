using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Parses the manual-upload import sheet — a single-sheet .xlsx workbook of label/value rows
// mirroring the fields a facility would otherwise fill in step by step online.
public interface IManualUploadTemplateService
{
    // Parses an uploaded workbook and validates every recognized cell. Never writes anything —
    // the frontend patches its own draft on acceptance and the normal save path persists it.
    Task<ImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
