using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Encounter;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Handles PUT /onboarding: splits the submitted draft between its owners and writes only the
// section belonging to the step being saved.
//
// Only the section for currentStepId is written to Link. The request carries the whole
// FacilityDraft, but writing every section on every save is what turns a stale tab into cross-step
// data loss: two tabs load at T0, A saves step 5 at T1, B saves step 7 at T2 still holding its T0
// copy, and B's stale step-5 values overwrite A's. Scoping means a stale client can only overwrite
// its own step, which it owns.
//
// The write lock is a separate mechanism for a separate hazard — two requests interleaving inside
// one read-modify-write cycle. It does nothing for staleness, and scoping does nothing for
// interleaving. Both are needed.
//
// currentStepId means the step whose data this payload carries, sent before the transition is
// applied. If a client ever sends it post-transition, the BFF scopes the write to the step the user
// is entering while the payload holds the step they're leaving, and that step's values are silently
// never written.
public sealed class OnboardingWriteService : IOnboardingWriteService
{
    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IOnboardingDraftStore _draftStore;
    private readonly IOnboardingReadService _readService;
    private readonly IFacilityGateway _facilityGateway;
    private readonly ICensusConfigurationGateway _censusGateway;
    private readonly ISftpConfigurationGateway _sftpConfigurationGateway;
    private readonly IPatientListGateway _patientListGateway;
    private readonly IFacilityAdministrationService _facilityAdministrationService;
    private readonly IOrganizationLocationConfigurationGateway _organizationLocationGateway;
    private readonly IEncounterMappingService _encounterMappingService;
    private readonly IHslocMappingService _hslocMappingService;
    private readonly IPatientsOfInterestService _patientsOfInterestService;
    private readonly IReportingService _reportingService;
    private readonly IFhirConfigurationGateway _fhirGateway;
    private readonly IFacilityWriteLock _writeLock;
    private readonly ILogger<OnboardingWriteService> _logger;

    public OnboardingWriteService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IOnboardingDraftStore draftStore,
        IOnboardingReadService readService,
        IFacilityGateway facilityGateway,
        ICensusConfigurationGateway censusGateway,
        ISftpConfigurationGateway sftpConfigurationGateway,
        IPatientListGateway patientListGateway,
        IFacilityAdministrationService facilityAdministrationService,
        IOrganizationLocationConfigurationGateway organizationLocationGateway,
        IEncounterMappingService encounterMappingService,
        IHslocMappingService hslocMappingService,
        IPatientsOfInterestService patientsOfInterestService,
        IReportingService reportingService,
        IFhirConfigurationGateway fhirGateway,
        IFacilityWriteLock writeLock,
        ILogger<OnboardingWriteService> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _draftStore = draftStore;
        _readService = readService;
        _facilityGateway = facilityGateway;
        _censusGateway = censusGateway;
        _sftpConfigurationGateway = sftpConfigurationGateway;
        _patientListGateway = patientListGateway;
        _facilityAdministrationService = facilityAdministrationService;
        _organizationLocationGateway = organizationLocationGateway;
        _encounterMappingService = encounterMappingService;
        _hslocMappingService = hslocMappingService;
        _patientsOfInterestService = patientsOfInterestService;
        _reportingService = reportingService;
        _fhirGateway = fhirGateway;
        _writeLock = writeLock;
        _logger = logger;
    }

    public async Task<DraftEnvelopeResponse> SaveAsync(FacilityDraftResponse draft, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var stepId = draft.CurrentStepId;

        await using (var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken))
        {
            var facility = await SaveWorkflowStateAsync(facilityId, stepId, draft, cancellationToken);
            await WriteStepSectionAsync(facility, stepId, draft, cancellationToken);
            await writeLock.CommitAsync(cancellationToken);
        }

        // Read back rather than echoing the request, so the response reflects what the owning
        // services actually hold and a value Link normalised or rejected shows up immediately.
        return await _readService.GetAsync(cancellationToken);
    }

    // Writes whichever sections a validated manual-upload import sheet held - unlike SaveAsync,
    // this can touch several sections in one call (an import sheet covers Fhir, Census, LocationOrg,
    // Hsloc and Encounter at once) rather than being scoped to a single step. Only called once
    // ManualUploadTemplateService has already validated the sheet (Accepted=true) - nothing here
    // validates again. Returns the re-read draft so the caller reports back what was actually saved,
    // not what was parsed.
    public async Task<ImportSaveResult> SaveImportedFieldsAsync(ImportedFields fields, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var sftpCredentialsSaved = false;
        bool? fhirConnectionTested = null;
        var sectionErrors = new List<ImportSectionSaveError>();

        void RecordFailure(string section, string? detail)
        {
            if (detail is not null)
            {
                sectionErrors.Add(new ImportSectionSaveError { Section = section, Detail = detail });
            }
        }

        await using (var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken))
        {
            if (fields.Fhir is { } fhir)
            {
                var fhirSection = new FhirSection
                {
                    FhirServerBaseUrl = fhir.FhirServerBaseUrl,
                    MaxConcurrentRequests = fhir.MaxConcurrentRequests,
                    MaxRetries = fhir.MaxRetries,
                    MinAcquisitionPullTime = fhir.MinAcquisitionPullTime,
                    MaxAcquisitionPullTime = fhir.MaxAcquisitionPullTime,
                    LagDuration = fhir.LagDuration
                };
                var (fhirSaved, fhirDetail) = await TrySectionAsync(facilityId, "fhir", () => WriteFhirSectionAsync(facilityId, fhirSection, cancellationToken));
                RecordFailure("fhir", fhirDetail);

                // Test Connection is a BFF-only concern for the import path - the frontend never
                // triggers it itself here. Only worth attempting once the required fields actually
                // made it to Tenant; skipping (rather than testing an unsaved config) is what "if
                // not, keep it not tested" means. Explicitly recorded either way - including false -
                // so a stale "tested" flag from a previous, unrelated save never lingers after an
                // import that changed the URL but couldn't verify it.
                fhirConnectionTested = fhirSaved && await TestFhirConnectionQuietlyAsync(fhirSection.FhirServerBaseUrl!, cancellationToken);
                await UpdateFhirConnectionTestedAsync(facilityId, fhirConnectionTested.Value, cancellationToken);
            }

            if (fields.Census is { } census)
            {
                var (_, censusDetail) = await TrySectionAsync(facilityId, "census", () => WriteCensusSectionAsync(facilityId, new CensusSection
                {
                    PatientListIds = census.PatientListIds ?? new Dictionary<string, string>(),
                    SftpHost = census.SftpHost,
                    SftpPort = census.SftpPort,
                    SftpRemoteDirectory = census.SftpRemoteDirectory,
                    SftpRemoveAfterProcessing = census.SftpRemoveAfterProcessing,
                    AcquisitionFrequency = census.AcquisitionFrequency
                }, cancellationToken));
                RecordFailure("census", censusDetail);

                // Secrets: saved straight to Data Acquisition and never round-tripped anywhere else
                // (not persisted to the draft, not included in the response this method returns).
                if (!string.IsNullOrWhiteSpace(census.SftpUsername) && !string.IsNullOrWhiteSpace(census.SftpPassword))
                {
                    var (credsSaved, credsDetail) = await TrySectionAsync(facilityId, "census.sftpCredentials", () => _patientsOfInterestService.SaveSftpCredentialsAsync(new SftpCredentialsRequest
                    {
                        Username = census.SftpUsername,
                        Password = census.SftpPassword
                    }, cancellationToken));
                    sftpCredentialsSaved = credsSaved;
                    RecordFailure("census", credsDetail);
                }
            }

            if (fields.LocationOrg is { } locationOrg)
            {
                var (_, locationOrgDetail) = await TrySectionAsync(facilityId, "location-org", () => WriteLocationOrgSectionAsync(facilityId, new LocationOrgSection
                {
                    Method = locationOrg.Method,
                    ManagingOrganizationIds = locationOrg.ManagingOrganizationIds ?? [],
                    LocationTypes = locationOrg.LocationTypes?
                        .Select(t => new LocationTypeEntry { Code = t.Code, Alias = t.Alias })
                        .ToList() ?? [],
                    LocationIdentifiers = locationOrg.LocationIdentifiers?
                        .Select(i => new LocationIdentifierEntry { System = i.System, Code = i.Code })
                        .ToList() ?? [],
                    CustomFhirPath = locationOrg.CustomFhirPath
                }, cancellationToken));
                RecordFailure("location-org", locationOrgDetail);
            }

            if (fields.Hsloc?.Mappings is { Count: > 0 } hslocMappings)
            {
                var (_, hslocDetail) = await TrySectionAsync(facilityId, "hsloc", () => _hslocMappingService.SaveAsync(
                    hslocMappings.Select(m => new HslocMapping
                    {
                        SourceCode = m.SourceCode,
                        SourceDisplay = m.SourceDisplay,
                        HslocCode = m.HslocCode
                    }).ToList(),
                    cancellationToken));
                RecordFailure("hsloc", hslocDetail);
            }

            if (fields.Encounter?.Mappings is { Count: > 0 } encounterMappings)
            {
                var (_, encounterDetail) = await TrySectionAsync(facilityId, "encounter", () => _encounterMappingService.SaveAsync(
                    encounterMappings.Select(m => new EncounterMapping
                    {
                        System = m.System,
                        Code = m.Code,
                        EncounterType = m.EncounterType
                    }).ToList(),
                    cancellationToken));
                RecordFailure("encounter", encounterDetail);

                // CodeSystems is a BFF-only cache (see SaveWorkflowStateAsync's "encounter" case)
                // that OnboardingReadService.GetAsync always echoes back verbatim, never reconciled
                // against Normalization's own mappings - so leaving it untouched here would let a
                // stale system from a previous online session or import survive next to this
                // sheet's own systems forever. The sheet is the source of truth for this step on
                // import, same as ManualUploadStep's own client-side patch of codeSystems.
                var sheetCodeSystems = encounterMappings.Select(m => m.System).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                await UpdateEncounterCodeSystemsAsync(facilityId, sheetCodeSystems, cancellationToken);
            }

            await writeLock.CommitAsync(cancellationToken);
        }

        var envelope = await _readService.GetAsync(cancellationToken);
        return new ImportSaveResult
        {
            Draft = envelope.Draft ?? new FacilityDraftResponse(),
            SftpCredentialsSaved = sftpCredentialsSaved,
            FhirConnectionTested = fhirConnectionTested,
            SectionErrors = sectionErrors
        };
    }

    // Reachability only - swallows any failure into "not tested" rather than letting a flaky FHIR
    // server or a network hiccup take the rest of the import down with it. A quiet false here just
    // means the facility sees an untested connection on the FHIR step, same as if no one had
    // clicked Test Connection yet - not an import failure.
    private async Task<bool> TestFhirConnectionQuietlyAsync(string fhirServerBaseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _facilityAdministrationService.TestFhirConnectionAsync(fhirServerBaseUrl, cancellationToken);
            return result.Success;
        }
        catch (Exception ex) when (ex is InvalidOperationException or LinkServiceException)
        {
            _logger.LogWarning(ex, "Manual-upload auto Test Connection failed for base URL {FhirServerBaseUrl}.", fhirServerBaseUrl);
            return false;
        }
    }

    // Same merge-one-slice-only shape as SaveWorkflowStateAsync's own "fhir" case, just reachable
    // outside a step-scoped save: SaveImportedFieldsAsync can set this without currentStepId being
    // "fhir" at all, since an import touches every section in one call.
    private async Task UpdateFhirConnectionTestedAsync(string facilityId, bool connectionTested, CancellationToken cancellationToken)
    {
        var stored = await _draftStore.GetAsync(facilityId, cancellationToken);
        var state = stored.State with { Fhir = new FhirWorkflowState { ConnectionTested = connectionTested } };
        await _draftStore.SaveAsync(facilityId, new StoredDraft { State = state, UnlockedStepIds = stored.UnlockedStepIds }, cancellationToken);
    }

    // Same shape as UpdateFhirConnectionTestedAsync, for the "encounter" case: SaveImportedFieldsAsync
    // can set this without currentStepId being "encounter" at all, since an import touches every
    // section in one call.
    private async Task UpdateEncounterCodeSystemsAsync(string facilityId, List<string> codeSystems, CancellationToken cancellationToken)
    {
        var stored = await _draftStore.GetAsync(facilityId, cancellationToken);
        var state = stored.State with { Encounter = new EncounterWorkflowState { CodeSystems = codeSystems } };
        await _draftStore.SaveAsync(facilityId, new StoredDraft { State = state, UnlockedStepIds = stored.UnlockedStepIds }, cancellationToken);
    }

    // Each section a manual-upload sheet touches is written independently, so a precondition one
    // section's owning service enforces (Census patient lists needing a FHIR base URL already on
    // file; FHIR itself needing both pull times, not just the sheet's) never takes the rest of the
    // sheet's otherwise-valid sections down with it. ManualUploadTemplateService has already
    // validated every cell's own format - what's caught here is a cross-service precondition that
    // isn't, and can't be, checked from the sheet alone. Returns whether the write actually reached
    // the owning service (so callers that need to know - Fhir's own Test Connection follow-up,
    // Census's HasCredentials - don't have to re-derive it from what was merely parsed) plus a
    // human-readable reason on failure, so the facility sees why a value they entered didn't save
    // instead of just finding it blank later.
    private async Task<(bool Success, string? Detail)> TrySectionAsync(string facilityId, string section, Func<Task> write)
    {
        try
        {
            await write();
            return (true, null);
        }
        catch (InvalidOperationException ex)
        {
            // A BFF-side precondition the write itself enforces before calling out at all (e.g.
            // Census patient lists needing a FHIR base URL already on file).
            _logger.LogWarning(ex, "Manual-upload section {Section} for facility {FacilityId} was not saved.", section, facilityId);
            return (false, ex.Message);
        }
        catch (LinkServiceException ex)
        {
            // The downstream Link service itself rejected the write (e.g. Data Acquisition 400s an
            // Organization Identification payload it considers invalid) - same "don't let one
            // section's failure take the rest of the sheet down with it" policy as above, just a
            // different failure surface: this one only happens once a real network call is made.
            _logger.LogWarning(
                ex,
                "Manual-upload section {Section} for facility {FacilityId} was not saved: {Service}.{Operation} returned {StatusCode}.",
                section, facilityId, ex.Service, ex.Operation, ex.StatusCode);
            return (false, ExtractDetail(ex));
        }
        catch (InvalidFhirConfigurationException ex)
        {
            // Data Acquisition rejected facility-entered configuration (e.g. a patient list id it
            // considers invalid) - same policy as LinkServiceException above, just a distinct type
            // so the online save path can map it to its own translated message.
            _logger.LogWarning(ex, "Manual-upload section {Section} for facility {FacilityId} was not saved.", section, facilityId);
            return (false, ex.Message);
        }
    }

    private static string ExtractDetail(LinkServiceException ex) => LinkResponseHandler.ProblemDetail(ex.RawBody) ?? ex.Message;

    // Merges the saved step's workflow slice onto what's stored, leaving every other step alone —
    // same reason as the Link write: a whole-blob replace would let a stale tab wipe every other
    // step. Protects hsloc.mappings especially: draft-held until Normalization can accept it,
    // and potentially many mapped locations, not a cursor position.
    private async Task<NhsnFacility> SaveWorkflowStateAsync(string facilityId, string? stepId, FacilityDraftResponse draft, CancellationToken cancellationToken)
    {
        var stored = await _draftStore.GetAsync(facilityId, cancellationToken);
        var state = stored.State;

        // currentView is a pair with currentStepId, so it comes from the caller wholesale rather
        // than merged, and is validated: a currentView naming another step is incoherent, and
        // storing it would resume the user inside a drill-down of a step they aren't on.
        var currentView = draft.CurrentView;
        if (currentView is not null && !string.Equals(currentView.StepId, stepId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Discarding currentView for facility {FacilityId}: it names step {ViewStepId} but the save is for {StepId}.",
                facilityId, currentView.StepId, stepId ?? "none");
            currentView = null;
        }

        state = state with { CurrentView = currentView };

        state = stepId switch
        {
            "fhir" => state with { Fhir = new FhirWorkflowState { ConnectionTested = draft.Fhir.ConnectionTested } },

            "census" => state with { Census = new CensusWorkflowState { SftpConnectionTested = draft.Census.SftpConnectionTested } },

            "encounter" => state with { Encounter = new EncounterWorkflowState { CodeSystems = [.. draft.Encounter.CodeSystems] } },

            "manual-upload" => state with
            {
                ManualUpload = new ManualUploadWorkflowState
                {
                    UploadedFileName = draft.ManualUpload.UploadedFileName,
                    UploadedOn = draft.ManualUpload.UploadedOn
                }
            },

            "report" => state with
            {
                Report = new ReportWorkflowState
                {
                    PatientIds = [.. draft.Report.PatientIds],
                    LastRequestedReportId = draft.Report.LastRequestedReportId
                }
            },

            "report-results" => state with
            {
                ReportResults = new ReportResultsWorkflowState
                {
                    ViewingReportId = draft.ReportResults.ViewingReportId,
                    LatestStatus = draft.ReportResults.LatestStatus
                }
            },

            "reporting-plan" => state with { ReportingPlan = new ReportingPlanWorkflowState { Reviewed = draft.ReportingPlan.Reviewed } },

            // welcome, facility-info, location-org, hsloc, mrn-intake, complete: no
            // workflow slice of their own. Their data is configuration, or a BFF table written
            // through its own endpoint.
            _ => state
        };

        // Unioned, never assigned — the set only grows, so a stale tab always carries a subset and
        // assigning it would silently re-lock steps the user has already reached. The step being
        // saved is added regardless: standing on a step means having reached it.
        var unlocked = stored.UnlockedStepIds
            .Union(draft.UnlockedStepIds, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(stepId) && !unlocked.Contains(stepId, StringComparer.Ordinal))
        {
            unlocked.Add(stepId);
        }

        await _draftStore.SaveAsync(facilityId, new StoredDraft
        {
            State = state,
            UnlockedStepIds = unlocked
        }, cancellationToken);

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            // /userinfo normally provisions this row first, but a save could in principle arrive
            // before any session call. Create it rather than fail: the token is valid.
            facility = new NhsnFacility { FacilityId = facilityId, CreatedBy = _userContext.ExternalUserId };
            _dbContext.Facilities.Add(facility);
        }

        facility.CurrentStepId = draft.CurrentStepId;

        // A save means work is underway. Complete and CommitFailed belong to the completion
        // fan-out and must not be walked backwards by an ordinary step save.
        if (facility.OnboardingStatus == OnboardingStatus.NotStarted)
        {
            facility.OnboardingStatus = OnboardingStatus.InProgress;
        }

        facility.LastModifiedOn = DateTime.UtcNow;
        facility.LastModifiedBy = _userContext.ExternalUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return facility;
    }

    // Writes the configuration owned by the step being saved, and nothing else. Steps absent from
    // this switch own no configuration — workflow-only, or their data belongs to a BFF table
    // written through its own endpoint, or their Link owner isn't reachable through the SDK yet.
    private async Task WriteStepSectionAsync(NhsnFacility facility, string? stepId, FacilityDraftResponse draft, CancellationToken cancellationToken)
    {
        switch (stepId)
        {
            case "facility-info":
                var previousVendor = facility.Vendor;

                await _facilityGateway.SaveAsync(new FacilityInfo
                {
                    FacilityId = facility.FacilityId,
                    FacilityName = _userContext.FacilityName,
                    TimeZone = draft.FacilityInfo.TimeZone,
                    Vendor = draft.FacilityInfo.Vendor
                }, cancellationToken);

                // Mirrored onto the row so /userinfo and an outage-time read can still branch on
                // vendor. Tenant remains the system of record.
                await MirrorVendorAsync(facility, draft.FacilityInfo.Vendor, cancellationToken);

                if (previousVendor is not null && draft.FacilityInfo.Vendor is not null && previousVendor != draft.FacilityInfo.Vendor)
                {
                    await RevokeAccuracyAcknowledgementsAsync(cancellationToken);
                }
                break;

            case "census":
                if (!string.IsNullOrWhiteSpace(draft.Census.AcquisitionFrequency))
                {
                    await _censusGateway.SaveAcquisitionFrequencyAsync(facility.FacilityId, draft.Census.AcquisitionFrequency, cancellationToken);
                }

                var censusAcquisition = facility.Vendor is { } vendor ? VendorProfileCatalog.Find(vendor)?.CensusAcquisition : null;

                if (censusAcquisition == CensusAcquisition.PatientList && draft.Census.PatientListIds.Count > 0)
                {
                    await _patientListGateway.SaveConfigurationAsync(facility.FacilityId, draft.Census.PatientListIds, cancellationToken);
                }
                else if (censusAcquisition == CensusAcquisition.Sftp
                    && !string.IsNullOrWhiteSpace(draft.Census.SftpHost) && draft.Census.SftpPort is not null)
                {
                    // A facility can only have one census acquisition method in Data Acquisition — a
                    // facility switching from Epic to Cerner would otherwise fail to save with a
                    // stale FHIR List configuration still on record.
                    await _patientListGateway.DeleteConfigurationIfExistsAsync(facility.FacilityId, cancellationToken);

                    await _sftpConfigurationGateway.SaveConfigurationAsync(facility.FacilityId, new SftpConfig
                    {
                        Host = draft.Census.SftpHost,
                        Port = draft.Census.SftpPort.Value,
                        RemoteDirectory = draft.Census.SftpRemoteDirectory ?? "/",
                        RemoveAfterProcessing = draft.Census.SftpRemoveAfterProcessing ?? false
                    }, cancellationToken);
                }
                break;

            case "fhir":
                var previousFhirBaseUrl = (await _fhirGateway.GetAsync(facility.FacilityId, cancellationToken))?.FhirServerBaseUrl?.Trim();

                var fhirWritten = await WriteFhirSectionAsync(facility.FacilityId, draft.Fhir, cancellationToken);

                var newFhirBaseUrl = draft.Fhir.FhirServerBaseUrl?.Trim();
                if (fhirWritten
                    && !string.IsNullOrWhiteSpace(previousFhirBaseUrl)
                    && !string.IsNullOrWhiteSpace(newFhirBaseUrl)
                    && !string.Equals(previousFhirBaseUrl, newFhirBaseUrl, StringComparison.Ordinal))
                {
                    await RevokeAccuracyAcknowledgementsAsync(cancellationToken);
                }
                break;

            case "location-org":
                await WriteLocationOrgSectionAsync(facility.FacilityId, draft.LocationOrg, cancellationToken);
                break;

            case "encounter":
                await _encounterMappingService.SaveAsync(draft.Encounter.Mappings, cancellationToken);
                break;

            case "hsloc":
                // Same data HslocStep's own PUT /hsloc-mappings writes. That endpoint stays --
                // Report Results' inline "+ Add Mapping" needs a save callable outside the
                // onboarding step -- but the generic dirty-save flow (OnboardingProvider's
                // unsaved-changes prompt, transition auto-save) only ever calls the generic
                // PUT /onboarding, so this case is what makes THAT path actually persist HSLOC
                // edits instead of silently discarding them.
                await _hslocMappingService.SaveAsync(draft.Hsloc.Mappings, cancellationToken);
                break;

            default:
                // Workflow-only step, or one whose data belongs to a BFF table written through its
                // own endpoint.
                break;
        }
    }

    private async Task WriteCensusSectionAsync(string facilityId, CensusSection census, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(census.AcquisitionFrequency))
        {
            await _censusGateway.SaveAcquisitionFrequencyAsync(facilityId, census.AcquisitionFrequency, cancellationToken);
        }

        if (census.PatientListIds is { Count: > 0 })
        {
            // PatientListGateway.SaveConfigurationAsync embeds the facility's FHIR base URL in the
            // payload it sends Data Acquisition, so it throws if that isn't on file yet. The online
            // flow can't hit this - FHIR is step 5, Census step 6, gating.ts won't unlock the second
            // without the first already saved - but a manual-upload sheet can carry Census fields
            // without a complete FHIR section (nothing is required anymore, see
            // ManualUploadTemplateService), landing here before FHIR exists. That's a real, known
            // precondition gap, not a request failure, so it's logged and skipped rather than
            // thrown - same as WriteFhirSectionAsync does when ITS prerequisites are incomplete -
            // instead of failing the whole save (locationOrg/hsloc/encounter included) over one
            // section not being ready yet.
            try
            {
                await _patientListGateway.SaveConfigurationAsync(facilityId, census.PatientListIds, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Census patient list configuration for facility {FacilityId} was not saved: FHIR server info must be saved first.",
                    facilityId);
            }
        }

        if (!string.IsNullOrWhiteSpace(census.SftpHost) && census.SftpPort is not null)
        {
            // A facility can only have one census acquisition method in Data Acquisition — a
            // facility switching from Epic to Cerner would otherwise fail to save with a
            // stale FHIR List configuration still on record.
            await _patientListGateway.DeleteConfigurationIfExistsAsync(facilityId, cancellationToken);

            await _sftpConfigurationGateway.SaveConfigurationAsync(facilityId, new SftpConfig
            {
                Host = census.SftpHost,
                Port = census.SftpPort.Value,
                RemoteDirectory = census.SftpRemoteDirectory ?? "/",
                RemoveAfterProcessing = census.SftpRemoveAfterProcessing ?? false
            }, cancellationToken);
        }
    }

    private async Task WriteLocationOrgSectionAsync(string facilityId, LocationOrgSection locationOrg, CancellationToken cancellationToken)
    {
        await _organizationLocationGateway.SaveAsync(new OrganizationLocationConfigurationSave
        {
            FacilityId = facilityId,
            LocationOrg = locationOrg
        }, cancellationToken);
    }

    // Returns whether the configuration was actually written, not just attempted.
    private async Task<bool> WriteFhirSectionAsync(string facilityId, FhirSection fhir, CancellationToken cancellationToken)
    {
        // FhirServerBaseUrl, MaxConcurrentRequests and BOTH pull times are hard requirements of
        // FacilityAdministrationService.UpdateFhirServerInfoAsync itself - MinAcquisitionPullTime/
        // MaxAcquisitionPullTime are parsed with TimeSpan.TryParseExact("hh\:mm") and it throws
        // rather than defaulting when that fails, so an empty string is not a valid "not set" value
        // here the way it is for other optional fields. Only maxRetries has a real default (0 is a
        // valid value in its own 0-10 range check) - everything else in this guard is load-bearing,
        // not a leftover overly-strict check.
        if (string.IsNullOrWhiteSpace(fhir.FhirServerBaseUrl) ||
            fhir.MaxConcurrentRequests is null ||
            string.IsNullOrWhiteSpace(fhir.MinAcquisitionPullTime) ||
            string.IsNullOrWhiteSpace(fhir.MaxAcquisitionPullTime))
        {
            return false;
        }

        var (lagDays, lagHours, lagMinutes) = FacilityAdministrationService.ParseLagDuration(fhir.LagDuration);

        var result = await _facilityAdministrationService.UpdateFhirServerInfoAsync(facilityId, new UpdateFhirServerInfoRequest
        {
            FhirServerBaseUrl = fhir.FhirServerBaseUrl,
            MaxConcurrentRequests = fhir.MaxConcurrentRequests.Value,
            MaxRetries = fhir.MaxRetries ?? 0,
            MinAcquisitionPullTime = fhir.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = fhir.MaxAcquisitionPullTime,
            LagDays = lagDays,
            LagHours = lagHours,
            LagMinutes = lagMinutes
        }, cancellationToken);

        if (result is null)
        {
            _logger.LogWarning("Step fhir for facility {FacilityId}: Tenant has no facility record; FHIR configuration not written.", facilityId);
            return false;
        }

        return true;
    }

    // Shared by facility-info's vendor change and fhir's base URL change.
    private async Task RevokeAccuracyAcknowledgementsAsync(CancellationToken cancellationToken)
    {
        await _patientsOfInterestService.AcknowledgeCensusAsync(new AcknowledgementRequest
        {
            Accepted = false,
            StatementKey = "census-accuracy"
        }, cancellationToken);

        var latestReports = await _reportingService.ListReportsAsync(1, 1, cancellationToken);
        var latestReportId = latestReports.Items.FirstOrDefault()?.ReportId;
        if (latestReportId is not null)
        {
            await _reportingService.RecordReportAccuracyAcknowledgementAsync(
                latestReportId, false, "report-accuracy", cancellationToken);
        }
    }

    private async Task MirrorVendorAsync(NhsnFacility facility, EhrVendor? vendor, CancellationToken cancellationToken)
    {
        if (vendor is null || facility.Vendor == vendor)
        {
            return;
        }

        facility.Vendor = vendor;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
