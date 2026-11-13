using System.Text.Json;
using System.Xml;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;

namespace Link.UI.Services;

public static class FacilityFormRules
{
    public const int MaxFacilityNameLength = 200;
    public const int MaxReportIdLength = 128;
    public const int MaxEventLength = 200;

    private static readonly IReadOnlyList<string> IanaTimeZoneIds = BuildIanaTimeZones();

    public static IReadOnlyList<string> TimeZones(string? current)
    {
        if (string.IsNullOrWhiteSpace(current) || IanaTimeZoneIds.Contains(current, StringComparer.Ordinal))
            return IanaTimeZoneIds;

        var withCurrent = new List<string>(IanaTimeZoneIds.Count + 1) { current.Trim() };
        withCurrent.AddRange(IanaTimeZoneIds);
        return withCurrent;
    }

    public static bool IsValidFacilityId(string? facilityId, bool numericOnly)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return false;

        var value = facilityId.Trim();
        return numericOnly
            ? System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{1,5}$")
            : System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9-]+$");
    }

    public static string FacilityIdRule(bool numericOnly) =>
        numericOnly
            ? "Facility ID must be numeric and up to 5 digits."
            : "Facility ID must be alphanumeric (letters, numbers, hyphens).";

    public static bool IsIanaTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return false;

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim());
            return zone.HasIanaId;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static bool TryBuildFacility(
        FacilityEditInput input,
        bool dmrpEnabled,
        bool numericOnlyFacilityId,
        bool vendorListLoaded,
        Guid? currentVendorVersionId,
        IReadOnlySet<Guid> allowedVendorVersionIds,
        out FacilityModel? model,
        out string? error)
    {
        model = null;
        var facilityId = input.FacilityId?.Trim() ?? string.Empty;
        if (!IsValidFacilityId(facilityId, numericOnlyFacilityId))
        {
            error = FacilityIdRule(numericOnlyFacilityId);
            return false;
        }

        var facilityName = input.FacilityName?.Trim() ?? string.Empty;
        if (facilityName.Length == 0)
        {
            error = "Facility name is required.";
            return false;
        }

        if (facilityName.Length > MaxFacilityNameLength)
        {
            error = $"Facility name must be {MaxFacilityNameLength} characters or fewer.";
            return false;
        }

        var timeZone = input.TimeZone?.Trim() ?? string.Empty;
        if (!IsIanaTimeZone(timeZone))
        {
            error = "Timezone must be an IANA id such as America/Chicago.";
            return false;
        }

        if (!TryResolveVendor(
                input.VendorVersionId,
                vendorListLoaded,
                currentVendorVersionId,
                allowedVendorVersionIds,
                out var vendorVersionId,
                out error))
        {
            return false;
        }

        TenantScheduledReportConfig schedule;
        if (dmrpEnabled)
        {
            schedule = EmptySchedule();
        }
        else if (!TryParseReports(input.DailyReports, input.WeeklyReports, input.MonthlyReports, out schedule, out error))
        {
            return false;
        }

        model = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = timeZone,
            VendorVersionId = vendorVersionId,
            ScheduledReports = schedule
        };
        error = null;
        return true;
    }

    public static bool TryParseReports(
        string? daily,
        string? weekly,
        string? monthly,
        out TenantScheduledReportConfig schedule,
        out string? error)
    {
        if (!TryParseReportList(daily, "Daily", out var dailyIds, out error)
            || !TryParseReportList(weekly, "Weekly", out var weeklyIds, out error)
            || !TryParseReportList(monthly, "Monthly", out var monthlyIds, out error))
        {
            schedule = EmptySchedule();
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in dailyIds.Concat(weeklyIds).Concat(monthlyIds))
        {
            if (!seen.Add(id))
            {
                schedule = EmptySchedule();
                error = "Scheduled reports must be unique across daily, weekly, and monthly.";
                return false;
            }
        }

        schedule = new TenantScheduledReportConfig
        {
            Daily = dailyIds,
            Weekly = weeklyIds,
            Monthly = monthlyIds
        };
        error = null;
        return true;
    }

    public static bool TryNormalizeSchedules(
        IEnumerable<DispatchScheduleInput>? rows,
        out List<DispatchScheduleApiModel> schedules,
        out string? error)
    {
        schedules = new List<DispatchScheduleApiModel>();
        if (rows is null)
        {
            error = "Add at least one dispatch schedule.";
            return false;
        }

        foreach (var row in rows)
        {
            if (row.Remove)
                continue;

            var ev = row.Event?.Trim() ?? string.Empty;
            var duration = row.Duration?.Trim() ?? string.Empty;
            if (ev.Length == 0 && duration.Length == 0)
                continue;

            if (ev.Length == 0 || duration.Length == 0)
            {
                error = "Each dispatch schedule needs an event and a duration.";
                return false;
            }

            if (ev.Length > MaxEventLength)
            {
                error = $"Event must be {MaxEventLength} characters or fewer.";
                return false;
            }

            if (!IsDuration(duration))
            {
                error = "Duration must be an ISO-8601 duration such as PT10S.";
                return false;
            }

            schedules.Add(new DispatchScheduleApiModel { Event = ev, Duration = duration });
        }

        if (schedules.Count == 0)
        {
            error = "Add at least one dispatch schedule, or delete the configuration.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return false;

        try
        {
            XmlConvert.ToTimeSpan(duration.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static string ServiceMessage(string serviceName, int statusCode, string? rawBody)
    {
        var lead = statusCode == 0
            ? $"{serviceName} could not be reached."
            : $"{serviceName} returned HTTP {statusCode}.";

        var detail = ShortDetail(rawBody);
        return detail is null ? lead : lead + " " + detail;
    }

    public static string JoinReports(IEnumerable<string>? ids) =>
        ids is null ? string.Empty : string.Join(", ", ids.Where(id => !string.IsNullOrWhiteSpace(id)));

    public static TenantScheduledReportConfig EmptySchedule() => new()
    {
        Daily = Array.Empty<string>(),
        Weekly = Array.Empty<string>(),
        Monthly = Array.Empty<string>()
    };

    public static List<DispatchScheduleInput> WithBlankRow(IEnumerable<DispatchScheduleInput>? rows)
    {
        var list = rows?.Select(row => new DispatchScheduleInput
        {
            Event = row.Event,
            Duration = row.Duration,
            Remove = row.Remove
        }).ToList() ?? new List<DispatchScheduleInput>();

        if (list.Count == 0 || list[^1].Event?.Trim().Length > 0 || list[^1].Duration?.Trim().Length > 0)
            list.Add(new DispatchScheduleInput());

        return list;
    }

    private static bool TryResolveVendor(
        string? posted,
        bool vendorListLoaded,
        Guid? currentVendorVersionId,
        IReadOnlySet<Guid> allowedVendorVersionIds,
        out Guid? vendorVersionId,
        out string? error)
    {
        var text = posted?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            vendorVersionId = vendorListLoaded ? null : currentVendorVersionId;
            error = null;
            return true;
        }

        if (!Guid.TryParse(text, out var parsed) || parsed == Guid.Empty)
        {
            vendorVersionId = null;
            error = "Vendor version is not a valid id.";
            return false;
        }

        if (!vendorListLoaded)
        {
            if (currentVendorVersionId != parsed)
            {
                vendorVersionId = null;
                error = "Vendor list could not be loaded. Save again without changing the vendor.";
                return false;
            }

            vendorVersionId = currentVendorVersionId;
            error = null;
            return true;
        }

        if (!allowedVendorVersionIds.Contains(parsed))
        {
            vendorVersionId = null;
            error = "Select a vendor from the list.";
            return false;
        }

        vendorVersionId = parsed;
        error = null;
        return true;
    }

    private static bool TryParseReportList(string? text, string label, out string[] ids, out string? error)
    {
        ids = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = null;
            return true;
        }

        var parts = text.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part.Length > MaxReportIdLength || !System.Text.RegularExpressions.Regex.IsMatch(part, @"^[A-Za-z0-9._:-]+$"))
            {
                error = $"{label} report '{part}' is not a measure id.";
                return false;
            }

            parsed.Add(part);
        }

        ids = parsed.ToArray();
        error = null;
        return true;
    }

    private static string? ShortDetail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (text.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    text = detail.GetString() ?? string.Empty;
                else if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    text = title.GetString() ?? string.Empty;
                else
                    return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length == 0)
            return null;

        return text.Length > 300 ? text[..300] + "..." : text;
    }

    private static IReadOnlyList<string> BuildIanaTimeZones()
    {
        var zones = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (zone.HasIanaId)
                zones.Add(zone.Id);

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) && !string.IsNullOrWhiteSpace(iana))
                zones.Add(iana);
        }

        if (zones.Count == 0)
            zones.Add("America/Chicago");

        return zones.ToList();
    }
}
