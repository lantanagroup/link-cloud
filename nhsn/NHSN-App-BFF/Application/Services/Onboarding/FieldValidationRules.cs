using System.Globalization;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Field-correctness rules shared between the manual-upload Excel import
// (ManualUploadTemplateService) and the online onboarding step forms (OnboardingValidationService).
// Extracted after a bug where the two paths held separate copies of the same rule and drifted out
// of sync. One cap, one pattern, one place: neither caller should ever redefine one of these itself.
public static class FieldValidationRules
{
    public const int MaxConcurrentRequestsMin = 1;
    public const int MaxConcurrentRequestsCap = 8;
    public const int MaxRetriesMin = 0;
    public const int MaxRetriesCap = 10;
    public const int LagDaysMin = 0;
    public const int LagDaysMax = 59;
    public const int LagHoursMin = 0;
    public const int LagHoursMax = 23;
    public const int LagMinutesMin = 0;
    public const int LagMinutesMax = 59;
    public const int CensusFrequencyMinMinutes = 5;
    public const int CensusFrequencyMaxMinutes = 24 * 60;
    public const int SftpPortMin = 1;
    public const int SftpPortMax = 65535;

    // http://hl7.org/fhir/R4/datatypes.html#id
    public static readonly Regex FhirIdPattern = new("^[A-Za-z0-9\\-.]{1,64}$", RegexOptions.Compiled);

    // 24-hour HH:MM.
    public static readonly Regex PullTimePattern = new("^([01]\\d|2[0-3]):[0-5]\\d$", RegexOptions.Compiled);

    // Just an absolute http(s) URL - "https://fhir.com" is a valid value on its own, same as
    // "https://fhir.com/r4". A path is common (the template's own example is
    // "https://FHIR-HOSTNAME/fhir") but not required - a bare origin is still a real, reachable
    // URL, not something to reject as malformed.
    public static bool IsAbsoluteUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Hostname or IPv4/IPv6 literal, no scheme/path - what an SFTP client connects to, as opposed
    // to fhirBaseUrl which is a full HTTP(S) URL. Deliberately permissive (this isn't RFC 1123
    // validation): it exists to catch someone pasting a full sftp:// URL or a path into the Host
    // cell, not to reject every unusual-but-real hostname.
    public static readonly Regex SftpHostPattern = new("^[A-Za-z0-9]([A-Za-z0-9.\\-:]*[A-Za-z0-9])?$", RegexOptions.Compiled);

    public static bool IsValidSftpHost(string value) =>
        value.Trim().Length is > 0 and <= 253 && SftpHostPattern.IsMatch(value.Trim());

    // Both are assumed already individually valid "HH:mm" (24-hour, zero-padded) by the time this
    // runs - that format sorts correctly as a plain ordinal string, so no TimeSpan parsing needed.
    public static bool IsPullTimeOrderValid(string minPullTime, string maxPullTime) =>
        string.CompareOrdinal(minPullTime, maxPullTime) < 0;

    public static bool IsNonNegativeInteger(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0;

    public static bool IsIntInRange(string value, int min, int max) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && IsIntInRange(parsed, min, max);

    public static bool IsIntInRange(int value, int min, int max) => value >= min && value <= max;

    public static bool IsPullTime(string value) => PullTimePattern.IsMatch(value);

    public static bool IsFhirId(string value) => FhirIdPattern.IsMatch(value);

    public static int LagTotalMinutes(int days, int hours, int minutes) => days * 24 * 60 + hours * 60 + minutes;

    public static int CensusFrequencyTotalMinutes(int hours, int minutes) => hours * 60 + minutes;

    public static bool IsValidCensusFrequency(int hours, int minutes) =>
        IsIntInRange(CensusFrequencyTotalMinutes(hours, minutes), CensusFrequencyMinMinutes, CensusFrequencyMaxMinutes);
}
