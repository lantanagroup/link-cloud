using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Carries X-Metrics-Mode across the DataAcquisitionLog hop without a SQL column.
/// DataAcquisitionRequestedListener stashes performance mode by facility+reportTrackingId;
/// AcquisitionProcessingJob copies it onto ReadyToAcquire headers.
/// </summary>
public static class ReportMetricsModeCache
{
    public const string KeyPrefix = "metrics-mode:";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    public static string Key(string facilityId, string reportTrackingId) =>
        $"{KeyPrefix}{facilityId}:{reportTrackingId}";

    public static async Task RememberIfPerformanceAsync(
        ICacheService? cache,
        string? facilityId,
        string? reportTrackingId,
        Headers? headers,
        CancellationToken cancellationToken = default)
    {
        if (cache == null
            || string.IsNullOrWhiteSpace(facilityId)
            || string.IsNullOrWhiteSpace(reportTrackingId)
            || !KafkaHeaderHelper.IsPerformanceMode(headers))
        {
            return;
        }

        await cache.SetAsync(
            Key(facilityId, reportTrackingId),
            "performance",
            Ttl,
            ExpirationType.Absolute,
            cancellationToken);
    }

    public static async Task<string?> TryGetAsync(
        ICacheService? cache,
        string? facilityId,
        string? reportTrackingId,
        CancellationToken cancellationToken = default)
    {
        if (cache == null
            || string.IsNullOrWhiteSpace(facilityId)
            || string.IsNullOrWhiteSpace(reportTrackingId))
        {
            return null;
        }

        return await cache.GetAsync<string>(Key(facilityId, reportTrackingId), cancellationToken);
    }
}
