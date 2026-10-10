using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace Link.UI.Services;

/// <summary>
/// Sets <see cref="FacilityModel.IsTest"/> on facilities the automation store already owns.
/// A facility that is already marked is left alone. A missing facility is skipped.
/// The pass runs once at startup and can run again; both are safe.
/// </summary>
public sealed class TestFacilityBackfill : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AutomationRunReader _reader;
    private readonly ILogger<TestFacilityBackfill> _logger;

    public TestFacilityBackfill(
        IServiceScopeFactory scopes,
        AutomationRunReader reader,
        ILogger<TestFacilityBackfill> logger)
    {
        _scopes = scopes;
        _reader = reader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var load = await _reader.LoadOwnershipAsync(stoppingToken);
            if (!load.Reachable)
            {
                _logger.LogInformation("Test-facility backfill skipped because the automation store could not be read.");
                return;
            }

            var owned = AutomationMarkRules.Build(load.Runs, load.Tombstones);
            var marked = 0;
            var already = 0;
            var missing = 0;
            var failed = 0;
            using var scope = _scopes.CreateScope();
            var facilities = scope.ServiceProvider.GetRequiredService<IFacilityServiceClient>();
            foreach (var facilityId in owned.FacilityIds)
            {
                stoppingToken.ThrowIfCancellationRequested();
                switch (await MarkAsync(facilities, facilityId, stoppingToken))
                {
                    case BackfillStep.Marked:
                        marked++;
                        break;
                    case BackfillStep.Already:
                        already++;
                        break;
                    case BackfillStep.Missing:
                        missing++;
                        break;
                    default:
                        failed++;
                        break;
                }
            }

            _logger.LogInformation(
                "Test-facility backfill finished. Marked {Marked}, already set {Already}, missing {Missing}, failed {Failed}.",
                marked,
                already,
                missing,
                failed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test-facility backfill stopped.");
        }
    }

    private async Task<BackfillStep> MarkAsync(
        IFacilityServiceClient facilities,
        string facilityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await facilities.GetAsync(facilityId, cancellationToken);
            if (existing.StatusCode == StatusCodes.Status404NotFound)
                return BackfillStep.Missing;
            if (!existing.IsSuccessStatusCode || existing.Body is null)
                return BackfillStep.Failed;
            if (existing.Body.IsTest)
                return BackfillStep.Already;

            existing.Body.IsTest = true;
            var updated = await facilities.UpdateAsync(facilityId, existing.Body, cancellationToken);
            return updated.IsSuccessStatusCode ? BackfillStep.Marked : BackfillStep.Failed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test-facility backfill could not update one facility.");
            return BackfillStep.Failed;
        }
    }

    private enum BackfillStep
    {
        Marked,
        Already,
        Missing,
        Failed
    }
}
