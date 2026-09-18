namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// What SaveImportedFieldsAsync reports back to ManualUploadTemplateService, alongside the re-read
// draft - the two flags cover outcomes that never show up in FacilityDraftResponse itself:
// SftpCredentialsSaved because secrets are never part of that shape, and FhirConnectionTested
// because it's workflow state (OnboardingDraftState.FhirWorkflowState), not configuration.
public sealed record ImportSaveResult
{
    public required FacilityDraftResponse Draft { get; init; }

    // Whether SaveSftpCredentialsAsync actually succeeded, not just whether the sheet had values
    // for both fields - a sheet can name working-looking credentials that Data Acquisition still
    // rejects (e.g. no SFTP config on file yet for them to attach to).
    public bool SftpCredentialsSaved { get; init; }

    // Whether the FHIR section was both saved AND found reachable, tested automatically right after
    // the save - see OnboardingWriteService.SaveImportedFieldsAsync. Null when there was no Fhir
    // section to save at all.
    public bool? FhirConnectionTested { get; init; }

    // Every section that validated fine on the sheet but failed once a real call to its owning Link
    // service was made (e.g. Data Acquisition rejecting an SFTP host as not a valid hostname). The
    // sheet itself was well-formed - ManualUploadTemplateService.ImportAsync still reports
    // Accepted=true and applies whatever DID save - but the facility needs to see this to know
    // those particular values didn't take, rather than silently finding a blank field later.
    public IReadOnlyList<ImportSectionSaveError> SectionErrors { get; init; } = [];
}

public sealed record ImportSectionSaveError
{
    // Matches ImportCellError.Section - the onboarding StepId the failed section belongs to.
    public required string Section { get; init; }

    // The downstream service's own explanation, extracted from its response where possible.
    public required string Detail { get; init; }
}
