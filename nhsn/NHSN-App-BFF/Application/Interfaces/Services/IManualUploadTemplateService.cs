using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Generates and parses the manual-upload import sheet — a single-sheet .xlsx workbook of
// label/value rows mirroring the fields a facility would otherwise fill in step by step online.
public interface IManualUploadTemplateService
{
    // Builds a workbook pre-filled with the facility's current draft values, so a partially
    // completed online session can be finished offline without re-entering what is already known.
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);

    // Parses an uploaded workbook and validates every recognized cell. Never writes anything —
    // the frontend patches its own draft on acceptance and the normal save path persists it.
    Task<ImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
