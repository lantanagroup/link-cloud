using System.Text.RegularExpressions;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

internal sealed class CensusConfigurationGateway : ICensusConfigurationGateway
{
    private const string ServiceName = "Census";

    private readonly ICensusServiceClient _censusClient;

    public CensusConfigurationGateway(ICensusServiceClient censusClient)
    {
        _censusClient = censusClient;
    }

    public async Task<string?> GetAcquisitionFrequencyAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _censusClient.GetCensusConfigAsync(facilityId, cancellationToken);
        var config = LinkResponseHandler.Optional(response, ServiceName, nameof(GetAcquisitionFrequencyAsync));

        return AcquisitionFrequencyCronConverter.ToDuration(config?.ScheduledTrigger);
    }

    public async Task SaveAcquisitionFrequencyAsync(string facilityId, string acquisitionFrequency, CancellationToken cancellationToken = default)
    {
        var scheduledTrigger = AcquisitionFrequencyCronConverter.ToCron(acquisitionFrequency);

        var response = await _censusClient.GetCensusConfigAsync(facilityId, cancellationToken);
        var current = LinkResponseHandler.Optional(response, ServiceName, nameof(SaveAcquisitionFrequencyAsync));

        if (current is null)
        {
            var created = new CensusConfigApiModel { FacilityId = facilityId, ScheduledTrigger = scheduledTrigger };
            var createResponse = await _censusClient.CreateCensusConfigAsync(created, cancellationToken);
            LinkResponseHandler.Require(createResponse, ServiceName, nameof(SaveAcquisitionFrequencyAsync));
            return;
        }

        // Enabled is carried through from the fetched instance and never assigned - it is the
        // arming switch, set only by the completion fan-out.
        current.ScheduledTrigger = scheduledTrigger;
        var updateResponse = await _censusClient.UpdateCensusConfigAsync(facilityId, current, cancellationToken);
        LinkResponseHandler.Require(updateResponse, ServiceName, nameof(SaveAcquisitionFrequencyAsync));
    }
}

// Census stores acquisition cadence as a Quartz cron trigger — CensusConfigController rejects
// anything CronExpression.IsValidExpression doesn't accept, and CensusSchedulingRepository only
// ever builds a CronTrigger (WithCronSchedule), never an interval-based SimpleTrigger. The
// onboarding UI, though, collects cadence as an hours+minutes interval and represents it as an
// ISO-8601 duration ("PT4H30M") — see duration.ts. A CronTrigger fires at specific clock times
// rather than N minutes after its last fire, so only intervals that evenly divide an hour
// (under 60 minutes) or a day (60 minutes and up) can be reproduced exactly; anything else is
// rounded to the nearest whole hour the trigger *can* express.
internal static class AcquisitionFrequencyCronConverter
{
    private static readonly Regex DurationPattern = new(@"^PT(\d+)H(\d+)M$", RegexOptions.Compiled);
    private static readonly Regex MinuteCronPattern = new(@"^0 0/(\d{1,2}) \* \* \* \?$", RegexOptions.Compiled);
    private static readonly Regex HourCronPattern = new(@"^0 0 0/(\d{1,2}) \* \* \?$", RegexOptions.Compiled);

    public static string? ToCron(string? isoDuration)
    {
        var match = isoDuration is not null ? DurationPattern.Match(isoDuration) : null;
        if (match is not { Success: true })
        {
            return isoDuration;
        }

        var totalMinutes = Math.Max(1, int.Parse(match.Groups[1].Value) * 60 + int.Parse(match.Groups[2].Value));

        if (totalMinutes < 60)
        {
            return $"0 0/{totalMinutes} * * * ?";
        }

        var hours = (int)Math.Round(totalMinutes / 60.0, MidpointRounding.AwayFromZero);
        return hours >= 24 ? "0 0 0 * * ?" : $"0 0 0/{hours} * * ?";
    }

    public static string? ToDuration(string? cron)
    {
        if (cron is null)
        {
            return null;
        }

        var minuteMatch = MinuteCronPattern.Match(cron);
        if (minuteMatch.Success)
        {
            return $"PT0H{minuteMatch.Groups[1].Value}M";
        }

        var hourMatch = HourCronPattern.Match(cron);
        if (hourMatch.Success)
        {
            return $"PT{hourMatch.Groups[1].Value}H0M";
        }

        // Not a shape this converter produces (hand-authored via fixtures/automation, or once
        // matched a rounding case this converter no longer emits) - nothing to translate back
        // into an interval.
        return null;
    }
}
