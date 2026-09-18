using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IOnboardingWriteService
{
    // Saves the draft: workflow state always, plus the configuration for the step named by
    // currentStepId and no other section. Returns the re-read envelope, so the caller sees what the
    // owning services actually hold.
    Task<DraftEnvelopeResponse> SaveAsync(FacilityDraftResponse draft, CancellationToken cancellationToken = default);

    // Writes whichever sections a validated manual-upload import sheet held (ImportedFields -
    // ManualUploadTemplateService.ImportAsync), which can span several sections in one call rather
    // than one step's worth. If the sheet included a Fhir section and it saved successfully, this
    // also tests the connection automatically and records the result - Test Connection is a BFF-only
    // concern here, never triggered from the frontend for an import. Returns the re-read draft plus
    // outcomes that don't show up in it, so the caller reports what was actually saved rather than
    // what was parsed.
    Task<ImportSaveResult> SaveImportedFieldsAsync(ImportedFields fields, CancellationToken cancellationToken = default);
}
