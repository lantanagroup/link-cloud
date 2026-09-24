using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Automation;

/// <summary>
/// After FHIR upload, Data Acquisition issues patient-scoped <c>search</c> queries.
/// HAPI can acknowledge a transaction POST before those searches are indexed, which
/// then records as completed acquisition logs with <c>acquired=0</c>. Wait until a
/// sample of uploaded patients is GET-able and searchable before starting the pipeline.
/// </summary>
public static class FhirAcquisitionReadiness
{
    public const int DefaultSampleSize = 3;

    public sealed record ReadinessProbe(string PatientId, string? SearchResourceType, int ExpectedSearchCount);

    public static IReadOnlyList<ReadinessProbe> BuildProbes(GenerationManifest manifest, int maxPatients = DefaultSampleSize)
    {
        var probes = new List<ReadinessProbe>();
        foreach (var patientId in manifest.ExpectedSubmittedPatientIds())
        {
            if (string.IsNullOrWhiteSpace(patientId))
                continue;

            var searchType = FirstSearchableType(manifest, patientId, out var expectedCount);
            probes.Add(new ReadinessProbe(patientId, searchType, expectedCount));
            if (probes.Count >= maxPatients)
                break;
        }

        return probes;
    }

    public static async Task WaitAsync(
        FhirDataLoader loader,
        IAutomationOutput output,
        GenerationManifest manifest,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var probes = BuildProbes(manifest);
        if (probes.Count == 0)
        {
            output.WriteLine("[FHIR] No submitted patients to verify after upload.");
            return;
        }

        var maxWait = timeout ?? TimeSpan.FromSeconds(60);
        output.WriteLine(
            $"[FHIR] Waiting up to {maxWait.TotalSeconds:F0}s for {probes.Count} uploaded patient(s) to be searchable before Data Acquisition.");

        foreach (var probe in probes)
            await WaitForProbeAsync(loader, output, probe, maxWait, cancellationToken);
    }

    private static string? FirstSearchableType(GenerationManifest manifest, string patientId, out int expectedCount)
    {
        var preferred = new[] { "Observation", "Encounter", "Condition" };
        Dictionary<string, int> counts;
        if (manifest.ResourceCountsByPatientType.TryGetValue(patientId, out var byType) && byType.Count > 0)
        {
            counts = new Dictionary<string, int>(byType, StringComparer.OrdinalIgnoreCase);
        }
        else if (manifest.ResourceKeysByPatient.TryGetValue(patientId, out var keys) && keys.Count > 0)
        {
            counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                var slash = key.IndexOf('/');
                var type = slash > 0 ? key[..slash] : key;
                if (string.IsNullOrWhiteSpace(type))
                    continue;
                counts[type] = counts.TryGetValue(type, out var current) ? current + 1 : 1;
            }
        }
        else
        {
            expectedCount = 0;
            return null;
        }

        foreach (var type in preferred)
        {
            if (counts.TryGetValue(type, out var count) && count > 0)
            {
                expectedCount = count;
                return type;
            }
        }

        expectedCount = 0;
        return null;
    }

    private static async Task WaitForProbeAsync(
        FhirDataLoader loader,
        IAutomationOutput output,
        ReadinessProbe probe,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(5);
        var attempt = 0;

        while (DateTime.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                if (!await loader.PatientExistsAsync(probe.PatientId, cancellationToken))
                {
                    output.WriteLine(
                        $"[FHIR] Patient/{probe.PatientId} not readable yet (check {attempt}, elapsed {(DateTime.UtcNow - started).TotalSeconds:F1}s).");
                }
                else if (probe.SearchResourceType == null)
                {
                    output.WriteLine($"[FHIR] Patient/{probe.PatientId} is readable.");
                    return;
                }
                else
                {
                    var count = await loader.SearchResourceCountAsync(
                        probe.SearchResourceType,
                        new Dictionary<string, string> { ["patient"] = probe.PatientId },
                        cancellationToken);

                    if (count > 0)
                    {
                        output.WriteLine(
                            $"[FHIR] Patient/{probe.PatientId} is searchable ({probe.SearchResourceType} search count={count}).");
                        return;
                    }

                    output.WriteLine(
                        $"[FHIR] Patient/{probe.PatientId} is readable but {probe.SearchResourceType} search still returns 0 " +
                        $"(generated {probe.ExpectedSearchCount}; check {attempt}, elapsed {(DateTime.UtcNow - started).TotalSeconds:F1}s).");
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                output.WriteLine(
                    $"[FHIR] Readiness check for Patient/{probe.PatientId} failed (check {attempt}): {ex.Message}");
            }

            var remaining = timeout - (DateTime.UtcNow - started);
            if (remaining <= TimeSpan.Zero)
                break;

            var nextDelay = delay <= remaining ? delay : remaining;
            await Task.Delay(nextDelay, cancellationToken);
            delay = delay + TimeSpan.FromSeconds(1) <= maxDelay ? delay + TimeSpan.FromSeconds(1) : maxDelay;
        }

        var searchHint = probe.SearchResourceType == null
            ? "Patient GET"
            : $"{probe.SearchResourceType} search (generated {probe.ExpectedSearchCount})";
        throw new TimeoutException(
            $"Timed out waiting for FHIR {searchHint} to become ready for patient '{probe.PatientId}' after {timeout.TotalSeconds:F0}s. " +
            "Data Acquisition would likely complete with acquired=0.");
    }
}
