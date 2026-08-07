using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Automation.UI.Services;

public sealed class PatientReplacementManager : IDisposable
{
    private readonly AutomationConfig _automationConfig;
    private readonly ILogger<PatientReplacementManager> _logger;
    private readonly ConcurrentDictionary<Guid, PatientReplacementOperation> _operations = new();
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CompletedOperationRetention = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CompletedOperationCleanupInterval = TimeSpan.FromMinutes(1);
    private readonly Timer _cleanupTimer;

    public PatientReplacementManager(
        IOptions<AutomationConfig> automationConfig,
        ILogger<PatientReplacementManager> logger)
    {
        _automationConfig = automationConfig.Value;
        _logger = logger;
        _cleanupTimer = new Timer(static state =>
        {
            var manager = (PatientReplacementManager)state!;
            try
            {
                manager.EvictCompletedOperations();
            }
            catch (Exception ex)
            {
                manager._logger.LogWarning(ex, "Failed periodic cleanup of completed patient replacement operations.");
            }
        }, this, CompletedOperationCleanupInterval, CompletedOperationCleanupInterval);
    }

    public Guid Start(
        string patientId,
        IReadOnlyList<string> resourcesToDelete,
        IReadOnlyList<(string Name, string Json)> replayBundles,
        CancellationToken ct = default)
    {
        EvictCompletedOperations();

        var operation = new PatientReplacementOperation(Guid.NewGuid(), patientId, replayBundles.Count);
        _operations[operation.Id] = operation;

        _ = Task.Run(async () =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(OperationTimeout);
            await ExecuteAsync(operation, resourcesToDelete, replayBundles, timeoutCts.Token);
        }, CancellationToken.None);

        return operation.Id;
    }

    public PatientReplacementStatus? GetStatus(Guid operationId)
    {
        EvictCompletedOperations();

        return _operations.TryGetValue(operationId, out var operation)
            ? operation.GetStatus()
            : null;
    }

    private async Task ExecuteAsync(
        PatientReplacementOperation operation,
        IReadOnlyList<string> resourcesToDelete,
        IReadOnlyList<(string Name, string Json)> replayBundles,
        CancellationToken ct)
    {
        try
        {
            var loader = new FhirDataLoader(
                _automationConfig.FhirServerBase,
                _automationConfig.FhirServerOAuth,
                _automationConfig.FhirServerBasicAuth);

            operation.Update("purging", "Requesting FHIR resource expunge.");
            var purge = await loader.DeleteResourcesWithExpungeAsync(resourcesToDelete, ct);
            operation.SetPurgeResult(purge.Succeeded, purge.Failed);

            operation.Update("waiting-for-purge", "Waiting for the patient to be removed from the FHIR server.");
            await loader.WaitForPatientDeletionAsync(
                operation.PatientId,
                message => operation.Update("waiting-for-purge", message),
                OperationTimeout,
                ct);

            operation.Update("uploading", $"Uploading {replayBundles.Count} replay bundle(s) to the FHIR server.");
            var output = new RunAutomationOutput(message => operation.Update("uploading", message));
            var replaySucceeded = await loader.UploadBundlesSequentiallyAsync(
                output,
                replayBundles,
                $"[replace:{operation.PatientId}] ");

            if (!replaySucceeded)
            {
                operation.Fail("Failed to replay the uploaded bundle after FHIR purge completion.");
                return;
            }

            operation.Complete($"Successfully replaced FHIR-server data for patient '{operation.PatientId}'.");
        }
        catch (OperationCanceledException)
        {
            operation.Fail($"FHIR patient replacement timed out or was canceled for patient '{operation.PatientId}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR patient replacement {OperationId} failed for patient '{PatientId}'.", operation.Id, operation.PatientId);
            operation.Fail(ex.Message);
        }
    }

    private void EvictCompletedOperations()
    {
        var cutoff = DateTimeOffset.UtcNow - CompletedOperationRetention;
        foreach (var kvp in _operations)
        {
            if (kvp.Value.TryGetCompletedAt(out var completedAt) && completedAt <= cutoff)
            {
                _operations.TryRemove(kvp.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

public sealed record PatientReplacementStatus(
    Guid OperationId,
    string State,
    string Message,
    bool IsComplete,
    bool Succeeded,
    int DeletedSucceeded,
    int DeletedFailed,
    int ReplayBundleCount);

internal sealed class PatientReplacementOperation(Guid id, string patientId, int replayBundleCount)
{
    private readonly object _sync = new();
    private string _state = "queued";
    private string _message = "Replacement request queued.";
    private bool _isComplete;
    private bool _succeeded;
    private int _deletedSucceeded;
    private int _deletedFailed;
    private DateTimeOffset? _completedAt;

    public Guid Id { get; } = id;
    public string PatientId { get; } = patientId;

    public void Update(string state, string message)
    {
        lock (_sync)
        {
            _state = state;
            _message = message;
        }
    }

    public void SetPurgeResult(int succeeded, int failed)
    {
        lock (_sync)
        {
            _deletedSucceeded = succeeded;
            _deletedFailed = failed;
        }
    }

    public void Complete(string message)
    {
        lock (_sync)
        {
            _state = "completed";
            _message = message;
            _succeeded = true;
            _isComplete = true;
            _completedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Fail(string message)
    {
        lock (_sync)
        {
            _state = "failed";
            _message = message;
            _isComplete = true;
            _completedAt = DateTimeOffset.UtcNow;
        }
    }

    public bool TryGetCompletedAt(out DateTimeOffset completedAt)
    {
        lock (_sync)
        {
            if (_completedAt.HasValue)
            {
                completedAt = _completedAt.Value;
                return true;
            }
        }

        completedAt = default;
        return false;
    }

    public PatientReplacementStatus GetStatus()
    {
        lock (_sync)
        {
            return new PatientReplacementStatus(
                Id,
                _state,
                _message,
                _isComplete,
                _succeeded,
                _deletedSucceeded,
                _deletedFailed,
                replayBundleCount);
        }
    }
}
