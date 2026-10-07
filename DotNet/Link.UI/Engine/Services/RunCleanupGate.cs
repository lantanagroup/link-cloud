using System.Text.Json;

namespace Link.UI.Services;

/// <summary>
/// Decides which of a run's own data to remove, and the short notice the run page shows.
/// The scenario flags stay the source of truth. A flag that is on applies only after success.
/// A flag that is off never removes that data.
/// </summary>
public static class RunCleanupGate
{
    public static bool ShouldCleanup(bool setting, bool succeeded) => setting && succeeded;

    public static bool ShouldDeleteServiceData(bool setting, bool succeeded, bool runOwnsFacility) =>
        ShouldCleanup(setting, succeeded) && runOwnsFacility;

    public static string? DataKeptNotice(bool cleanupFhirData, bool cleanupServiceData)
    {
        if (cleanupFhirData && cleanupServiceData)
            return "FHIR data and service data were kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.";

        if (cleanupServiceData)
            return "Service data was kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.";

        if (cleanupFhirData)
            return "FHIR data was kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.";

        return null;
    }

    /// <summary>
    /// Reads the camelCase scenario JSON stored with the run. A missing FHIR flag stays on.
    /// A missing service flag stays off. Unreadable JSON produces no notice.
    /// </summary>
    public static string? DataKeptNotice(string? runConfigurationJson)
    {
        if (string.IsNullOrWhiteSpace(runConfigurationJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(runConfigurationJson);
            var root = doc.RootElement;
            var fhir = ReadBool(root, "cleanupFhirData") ?? true;
            var service = ReadBool(root, "cleanupServiceData") ?? false;
            return DataKeptNotice(fhir, service);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? SuccessNotice(bool removedFhir, bool removedService, bool serviceKeptBecauseUnowned)
    {
        if (removedFhir && removedService)
            return "Removed this run's FHIR data and service data.";

        if (removedFhir && serviceKeptBecauseUnowned)
            return "Removed this run's FHIR data. Service data was kept because this run did not create the facility. Clean it up later from Cleanup, which acts on automation facilities only.";

        if (removedService)
            return "Removed this run's service data.";

        if (removedFhir)
            return "Removed this run's FHIR data.";

        if (serviceKeptBecauseUnowned)
            return "Service data was kept because this run did not create the facility. Clean it up later from Cleanup, which acts on automation facilities only.";

        return null;
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}
