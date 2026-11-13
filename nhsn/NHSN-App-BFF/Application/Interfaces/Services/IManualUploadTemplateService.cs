using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Parses the manual-upload import sheet — a single-sheet .xlsx workbook of label/value rows
// mirroring the fields a facility would otherwise fill in step by step online.
public interface IManualUploadTemplateService
{
    // Parses an uploaded workbook and validates every recognized cell. A fully valid sheet is
    // saved immediately, in this same call (see IOnboardingWriteService.SaveImportedFieldsAsync) —
    // the frontend then patches its own draft from the returned ImportResult.Fields, which reflects
    // what was actually saved, not merely what was parsed.
    Task<ImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
