using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Assembles FacilityDraft from its several owners.
//
// The UI receives one object and never learns which value came from where. This class owns that
// split, and it's the only place that knows a section can be missing because a service was
// unreachable rather than because nothing was configured.
//
// Sections fan out in parallel, each under its own deadline. Nothing is retried: retrying several
// parallel reads multiplies load on a downstream that's already failing, and the operation is
// user-initiated and cheap to repeat — the natural retry is a page refresh, with a person deciding
// whether it's worth it.
public sealed class OnboardingReadService : IOnboardingReadService
{
    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IOnboardingDraftStore _draftStore;
    private readonly IFacilityGateway _facilityGateway;
    private readonly IFhirConfigurationGateway _fhirGateway;
    private readonly ICensusConfigurationGateway _censusGateway;
    private readonly ISftpConfigurationGateway _sftpConfigurationGateway;
    private readonly IQueryDispatchGateway _queryDispatchGateway;
    private readonly IReportGateway _reportGateway;
    private readonly IAcknowledgementService _acknowledgementService;
    private readonly OnboardingReadSettings _settings;
    private readonly ILogger<OnboardingReadService> _logger;

    public OnboardingReadService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IOnboardingDraftStore draftStore,
        IFacilityGateway facilityGateway,
        IFhirConfigurationGateway fhirGateway,
        ICensusConfigurationGateway censusGateway,
        ISftpConfigurationGateway sftpConfigurationGateway,
        IQueryDispatchGateway queryDispatchGateway,
        IReportGateway reportGateway,
        IAcknowledgementService acknowledgementService,
        IOptions<OnboardingReadSettings> settings,
        ILogger<OnboardingReadService> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _draftStore = draftStore;
        _facilityGateway = facilityGateway;
        _fhirGateway = fhirGateway;
        _censusGateway = censusGateway;
        _sftpConfigurationGateway = sftpConfigurationGateway;
        _queryDispatchGateway = queryDispatchGateway;
        _reportGateway = reportGateway;
        _acknowledgementService = acknowledgementService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DraftEnvelopeResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(_settings.OverallTimeoutMs);

        var sources = new List<SectionSource>();

        // BFF-owned reads first. They are local, they cannot fail the way a Link call can, and the
        // facility row supplies currentStepId, which the assembled shape needs whether or not any
        // downstream answered.
        var facilityRow = await _dbContext.Facilities
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        var storedDraft = await _draftStore.GetAsync(facilityId, cancellationToken);
        sources.Add(Ok("workflow", "Bff"));

        var censusAccuracyAcknowledged = await _acknowledgementService.GetLatestAsync(
            facilityId, AcknowledgementKind.CensusAccuracy, cancellationToken: cancellationToken);

        // Fan out. Each section carries its own deadline so one hung service cannot hold the rest.
        var facilityInfoTask = ReadSectionAsync("facilityInfo", "Tenant",
            ct => _facilityGateway.GetAsync(facilityId, ct), overall.Token, cancellationToken);

        var fhirTask = ReadSectionAsync("fhir", "DataAcquisition",
            ct => _fhirGateway.GetAsync(facilityId, ct), overall.Token, cancellationToken);

        var censusTask = ReadSectionAsync("census", "Census",
            ct => _censusGateway.GetAcquisitionFrequencyAsync(facilityId, ct), overall.Token, cancellationToken);

        // lagDuration is a field within the fhir section, not a section of its own - its Source
        // is deliberately not added below, so a Query Dispatch outage doesn't mark the whole fhir
        // section Unavailable over one supplementary field. sftpConfig and hasCredentials are the
        // same relationship to the census section.
        var lagDurationTask = ReadSectionAsync("fhir", "QueryDispatch",
            ct => _queryDispatchGateway.GetLagDurationAsync(facilityId, ct), overall.Token, cancellationToken);

        var sftpConfigTask = ReadSectionAsync("census", "DataAcquisition",
            ct => _sftpConfigurationGateway.GetConfigurationAsync(facilityId, ct), overall.Token, cancellationToken);

        var hasCredentialsTask = ReadSectionAsync<bool?>("census", "DataAcquisition",
            async ct => await _sftpConfigurationGateway.GetHasCredentialsAsync(facilityId, ct), overall.Token, cancellationToken);

        var reportTask = ReadSectionAsync("report", "Report",
            ct => _reportGateway.GetLatestScheduleAsync(facilityId, ct), overall.Token, cancellationToken);

        await Task.WhenAll(facilityInfoTask, fhirTask, censusTask, lagDurationTask, sftpConfigTask, hasCredentialsTask, reportTask);

        var facilityInfo = await facilityInfoTask;
        var fhir = await fhirTask;
        var census = await censusTask;
        var lagDuration = await lagDurationTask;
        var sftpConfig = await sftpConfigTask;
        var hasCredentials = await hasCredentialsTask;
        var report = await reportTask;

        sources.Add(facilityInfo.Source);
        sources.Add(fhir.Source);
        sources.Add(census.Source);
        sources.Add(report.Source);

        return new DraftEnvelopeResponse
        {
            Draft = Assemble(facilityRow, storedDraft, facilityInfo.Value, fhir.Value, census.Value, lagDuration.Value,
                censusAccuracyAcknowledged, sftpConfig.Value, hasCredentials.Value, report.Value),
            CommitState = null, // Populated once the completion fan-out exists.
            Sources = sources
        };
    }

    private static FacilityDraftResponse Assemble(
        NhsnFacility? facility,
        StoredDraft stored,
        FacilityInfo? facilityInfo,
        FhirSection? fhir,
        string? acquisitionFrequency,
        string? lagDuration,
        bool? censusAccuracyAcknowledged,
        SftpConfig? sftpConfig,
        bool? hasCredentials,
        ReportScheduleSummary? report) => new()
        {
            SchemaVersion = DraftSchema.CurrentVersion,
            CurrentStepId = facility?.CurrentStepId,
            CurrentView = stored.State.CurrentView,
            UnlockedStepIds = stored.UnlockedStepIds,

            FacilityInfo = new FacilityInfoSection
            {
                TimeZone = facilityInfo?.TimeZone,

                // Tenant owns vendor; the Facilities column is a read cache, used only when Tenant
                // could not be reached so the UI can still branch on vendor during an outage.
                Vendor = facilityInfo?.Vendor ?? facility?.Vendor
            },

            ManualUpload = new ManualUploadSection
            {
                UploadedFileName = stored.State.ManualUpload.UploadedFileName,
                UploadedOn = stored.State.ManualUpload.UploadedOn
            },

            // The per-field merge: configuration from Data Acquisition, the flag from DraftJson,
            // the lag duration from Query Dispatch.
            Fhir = (fhir ?? new FhirSection()) with
            {
                ConnectionTested = stored.State.Fhir.ConnectionTested,
                LagDuration = lagDuration
            },

            Census = new CensusSection
            {
                SftpHost = sftpConfig?.Host,
                SftpPort = sftpConfig?.Port,
                SftpRemoteDirectory = sftpConfig?.RemoteDirectory,
                SftpRemoveAfterProcessing = sftpConfig?.RemoveAfterProcessing,
                HasCredentials = hasCredentials,
                AcquisitionFrequency = acquisitionFrequency,
                AccuracyAcknowledged = censusAccuracyAcknowledged
            },

            // LocationOrg: Data Acquisition has read methods for both resources
            // (GetOrganizationLocationConfigurationsAsync, GetOrganizationLocationMappingsAsync) —
            // not wired yet.
            // Encounter: owned by Normalization (SearchFacilityOperationsAsync), not Data
            // Acquisition — also not wired yet.
            LocationOrg = new LocationOrgSection(),
            Encounter = new EncounterSection(),

            Hsloc = new HslocSection { Mappings = stored.State.Hsloc.Mappings },

            // Measures and lastRequestedReportId both come from Report when a schedule exists —
            // stored.State.Report.LastRequestedReportId is only the fallback for a request that's
            // in flight and has no schedule yet.
            Report = new ReportSection
            {
                Measures = report?.Measures ?? [],
                PatientIds = stored.State.Report.PatientIds,
                LastRequestedReportId = report?.ReportId ?? stored.State.Report.LastRequestedReportId
            },

            ReportResults = new ReportResultsSection
            {
                ViewingReportId = stored.State.ReportResults.ViewingReportId,
                LatestStatus = stored.State.ReportResults.LatestStatus
            },

            ReportingPlan = new ReportingPlanSection { Reviewed = stored.State.ReportingPlan.Reviewed }
        };

    // Runs one section's read under its own deadline, converting any failure into a status rather
    // than letting it fail the whole request. A timeout and an error are reported identically —
    // from the user's side, "the service didn't answer" and "the service answered with an error"
    // are the same fact: that step can't be worked on right now.
    private async Task<SectionResult<T>> ReadSectionAsync<T>(
        string section,
        string origin,
        Func<CancellationToken, Task<T?>> read,
        CancellationToken overallToken,
        CancellationToken clientToken)
    {
        using var sectionCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
        sectionCts.CancelAfter(_settings.SectionTimeoutMs);

        try
        {
            var value = await read(sectionCts.Token);
            return new SectionResult<T>(value, Ok(section, origin));
        }
        catch (LinkServiceException ex)
        {
            _logger.LogWarning(ex,
                "Section {Section} unavailable from {Origin}. Status={StatusCode}; DownstreamTraceId={TraceId}.",
                section, origin, ex.StatusCode, ex.TraceId ?? "none");

            return new SectionResult<T>(default, new SectionSource
            {
                Section = section,
                Origin = origin,
                Status = SectionStatus.Unavailable,
                TraceId = ex.TraceId,
                Detail = DescribeFailure(origin, ex.StatusCode)
            });
        }
        catch (OperationCanceledException) when (!clientToken.IsCancellationRequested)
        {
            // Either the section's own deadline or the overall backstop fired. A cancellation
            // propagating from the client itself is a different thing — the user navigated away —
            // and is allowed to bubble.
            _logger.LogWarning("Section {Section} from {Origin} timed out after {TimeoutMs}ms.",
                section, origin, _settings.SectionTimeoutMs);

            return new SectionResult<T>(default, new SectionSource
            {
                Section = section,
                Origin = origin,
                Status = SectionStatus.Unavailable,
                Detail = $"{origin} did not respond within {_settings.SectionTimeoutMs}ms."
            });
        }
    }

    // LinkSdk reports StatusCode = 0 when there was no HTTP response at all — a refused connection,
    // DNS failure or dropped route. "returned 0" would send someone looking for an HTTP status that
    // doesn't exist, when the fact is the service wasn't reachable.
    private static string DescribeFailure(string origin, int statusCode) =>
        statusCode == 0
            ? $"{origin} could not be reached."
            : $"{origin} returned {statusCode}.";

    private static SectionSource Ok(string section, string origin) =>
        new() { Section = section, Origin = origin, Status = SectionStatus.Ok };

    private readonly record struct SectionResult<T>(T? Value, SectionSource Source);
}
