using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
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
    private readonly IFacilityWriteLock _writeLock;
    private readonly ILogger<OnboardingWriteService> _logger;

    public OnboardingWriteService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IOnboardingDraftStore draftStore,
        IOnboardingReadService readService,
        IFacilityGateway facilityGateway,
        ICensusConfigurationGateway censusGateway,
        IFacilityWriteLock writeLock,
        ILogger<OnboardingWriteService> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _draftStore = draftStore;
        _readService = readService;
        _facilityGateway = facilityGateway;
        _censusGateway = censusGateway;
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

    // Merges the saved step's workflow slice onto what's stored, leaving every other step alone.
    // Scoped for the same reason the Link write is: replacing the whole DraftJson blob from the
    // payload would let a stale tab wipe every other step's workflow state.
    //
    // hsloc.mappings is the case where that would actually hurt — it's contract-pending
    // configuration held here only until Normalization can own it, and a user may have mapped many
    // locations. That's real work, not a cursor position.
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

            "hsloc" => state with { Hsloc = new HslocWorkflowState { Mappings = [.. draft.Hsloc.Mappings] } },

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

            // welcome, facility-info, census, location-org, encounter, mrn-intake, complete: no
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
                await _facilityGateway.SaveAsync(new FacilityInfo
                {
                    FacilityId = facility.FacilityId,
                    TimeZone = draft.FacilityInfo.TimeZone,
                    Vendor = draft.FacilityInfo.Vendor
                }, cancellationToken);

                // Mirrored onto the row so /userinfo and an outage-time read can still branch on
                // vendor. Tenant remains the system of record.
                await MirrorVendorAsync(facility, draft.FacilityInfo.Vendor, cancellationToken);
                break;

            case "census":
                if (!string.IsNullOrWhiteSpace(draft.Census.AcquisitionFrequency))
                {
                    await _censusGateway.SaveAcquisitionFrequencyAsync(facility.FacilityId, draft.Census.AcquisitionFrequency, cancellationToken);
                }
                break;

            case "fhir":
            case "location-org":
                // Data Acquisition owns these, and its SDK client exposes no update operation on
                // any configuration resource yet. Workflow state above is still saved, so a user
                // keeps their place; the configuration write lands once the SDK has one.
                _logger.LogWarning(
                    "Step {StepId} for facility {FacilityId}: configuration not written. Data Acquisition writes are unavailable until the SDK exposes an update operation.",
                    stepId, facility.FacilityId);
                break;

            case "encounter":
                // Normalization owns this, not Data Acquisition — CreateOperationAsync already
                // exists. Not wired yet; workflow state above is still saved, so a user keeps
                // their place.
                _logger.LogWarning(
                    "Step {StepId} for facility {FacilityId}: configuration not written. Normalization write path exists but is not yet wired.",
                    stepId, facility.FacilityId);
                break;

            default:
                // Workflow-only step, or one whose data is written through its own endpoint.
                break;
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
