using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Runs once, from the MRN Identifier Intake step's "Complete Enrollment" button. Every gateway
// call here is one OnboardingReadService already makes to assemble the draft (or, for Census,
// the arming write ICensusConfigurationGateway.EnableAsync's doc comment reserves for exactly
// this fan-out) — nothing here talks to a Link service OnboardingReadService doesn't already
// know how to reach.
public sealed class OnboardingCompletionService : IOnboardingCompletionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IFacilityWriteLock _writeLock;
    private readonly IFacilityGateway _facilityGateway;
    private readonly IFhirConfigurationGateway _fhirConfigurationGateway;
    private readonly ICensusConfigurationGateway _censusConfigurationGateway;
    private readonly IQueryDispatchGateway _queryDispatchGateway;
    private readonly IReportGateway _reportGateway;
    private readonly ILogger<OnboardingCompletionService> _logger;

    public OnboardingCompletionService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IFacilityWriteLock writeLock,
        IFacilityGateway facilityGateway,
        IFhirConfigurationGateway fhirConfigurationGateway,
        ICensusConfigurationGateway censusConfigurationGateway,
        IQueryDispatchGateway queryDispatchGateway,
        IReportGateway reportGateway,
        ILogger<OnboardingCompletionService> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _writeLock = writeLock;
        _facilityGateway = facilityGateway;
        _fhirConfigurationGateway = fhirConfigurationGateway;
        _censusConfigurationGateway = censusConfigurationGateway;
        _queryDispatchGateway = queryDispatchGateway;
        _reportGateway = reportGateway;
        _logger = logger;
    }

    public async Task<CommitResultResponse> CompleteAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        await using var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken);

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            // A completion attempt implies every prior step already created this row; created
            // defensively rather than failed, the same reasoning OnboardingWriteService uses.
            facility = new NhsnFacility { FacilityId = facilityId, CreatedBy = _userContext.ExternalUserId };
            _dbContext.Facilities.Add(facility);
        }

        facility.OnboardingStatus = OnboardingStatus.Committing;
        facility.LastModifiedOn = DateTime.UtcNow;
        facility.LastModifiedBy = _userContext.ExternalUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var services = new List<CommitServiceResultResponse>
        {
            await RunStageAsync("Tenant", 1, () => VerifyTenantAsync(facilityId, cancellationToken)),
            await RunStageAsync("DataAcquisition", 1, () => VerifyDataAcquisitionAsync(facilityId, cancellationToken)),
            await RunStageAsync("Census", 2, () => _censusConfigurationGateway.EnableAsync(facilityId, cancellationToken)),
            await RunStageAsync("QueryDispatch", 2, () => VerifyQueryDispatchAsync(facilityId, cancellationToken)),
            await RunStageAsync("Report", 2, () => VerifyReportAsync(facilityId, cancellationToken))
        };

        var allCommitted = services.All(service => service.Status == "committed");
        var result = new CommitResultResponse { FacilityId = facilityId, Services = services };

        var commitRow = await _dbContext.OnboardingCommits.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (commitRow is null)
        {
            commitRow = new OnboardingCommit { FacilityId = facilityId };
            _dbContext.OnboardingCommits.Add(commitRow);
        }

        commitRow.ResultJson = JsonSerializer.Serialize(result);
        commitRow.UpdatedOn = DateTime.UtcNow;

        // Complete/CommitFailed are this fan-out's alone to write — an ordinary step save only ever
        // moves NotStarted -> InProgress and must not walk these back down.
        facility.OnboardingStatus = allCommitted ? OnboardingStatus.Complete : OnboardingStatus.CommitFailed;
        if (allCommitted)
        {
            facility.CompletedOn = DateTime.UtcNow;
        }
        facility.LastModifiedOn = DateTime.UtcNow;
        facility.LastModifiedBy = _userContext.ExternalUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await writeLock.CommitAsync(cancellationToken);

        if (!allCommitted)
        {
            _logger.LogWarning("Onboarding completion for facility {FacilityId} did not fully commit: {Detail}",
                facilityId, string.Join("; ", services.Where(s => s.Status != "committed").Select(s => $"{s.Service}={s.Status}")));
        }

        return result;
    }

    public async Task<CommitResultResponse?> GetCommitStateAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var row = await _dbContext.OnboardingCommits
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CommitResultResponse>(row.ResultJson, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Commit result for facility {FacilityId} could not be parsed.", facilityId);
            return null;
        }
    }

    private async Task VerifyTenantAsync(string facilityId, CancellationToken cancellationToken)
    {
        var facilityInfo = await _facilityGateway.GetAsync(facilityId, cancellationToken);
        if (facilityInfo is null)
        {
            throw new OnboardingStagePendingException("Facility Information has not been saved yet.");
        }
    }

    private async Task VerifyDataAcquisitionAsync(string facilityId, CancellationToken cancellationToken)
    {
        var fhir = await _fhirConfigurationGateway.GetAsync(facilityId, cancellationToken);
        if (fhir is null || string.IsNullOrWhiteSpace(fhir.FhirServerBaseUrl))
        {
            throw new OnboardingStagePendingException("FHIR Server Information has not been saved yet.");
        }
    }

    private async Task VerifyQueryDispatchAsync(string facilityId, CancellationToken cancellationToken)
    {
        var lagDuration = await _queryDispatchGateway.GetLagDurationAsync(facilityId, cancellationToken);
        if (string.IsNullOrWhiteSpace(lagDuration))
        {
            throw new OnboardingStagePendingException("FHIR Server Information's acquisition lag duration has not been saved yet.");
        }
    }

    private async Task VerifyReportAsync(string facilityId, CancellationToken cancellationToken)
    {
        var schedule = await _reportGateway.GetLatestScheduleAsync(facilityId, cancellationToken);
        if (schedule is null)
        {
            throw new OnboardingStagePendingException("No test report has been generated yet.");
        }
    }

    // Converts a stage's exception into its status rather than letting it fail the whole request —
    // mirrors OnboardingReadService.ReadSectionAsync's LinkServiceException handling, plus a
    // "pending" status for a prerequisite step that was never completed, which is not a downstream
    // failure.
    private static async Task<CommitServiceResultResponse> RunStageAsync(string service, int stage, Func<Task> action)
    {
        try
        {
            await action();
            return new CommitServiceResultResponse { Service = service, Stage = stage, Status = "committed" };
        }
        catch (OnboardingStagePendingException ex)
        {
            return new CommitServiceResultResponse { Service = service, Stage = stage, Status = "pending", Detail = ex.Message };
        }
        catch (LinkServiceException ex)
        {
            return new CommitServiceResultResponse
            {
                Service = service,
                Stage = stage,
                Status = "failed",
                Detail = DescribeFailure(service, ex.StatusCode)
            };
        }
    }

    // Mirrors OnboardingReadService.DescribeFailure — LinkSdk reports StatusCode = 0 when there was
    // no HTTP response at all, which "returned 0" would misdescribe as a status code.
    private static string DescribeFailure(string service, int statusCode) =>
        statusCode == 0 ? $"{service} could not be reached." : $"{service} returned {statusCode}.";

    private sealed class OnboardingStagePendingException(string message) : Exception(message);
}
