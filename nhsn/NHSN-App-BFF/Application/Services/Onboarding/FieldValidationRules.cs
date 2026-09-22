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
    public const int LagDurationCapMinutes = 59 * 24 * 60; // 59 days
    public const int CensusFrequencyMinMinutes = 5;
    public const int CensusFrequencyMaxMinutes = 24 * 60;
    public const int SftpPortMin = 1;
    public const int SftpPortMax = 65535;

    // http://hl7.org/fhir/R4/datatypes.html#id
    public static readonly Regex FhirIdPattern = new("^[A-Za-z0-9\\-.]{1,64}$", RegexOptions.Compiled);

    // 24-hour HH:MM.
    public static readonly Regex PullTimePattern = new("^([01]\\d|2[0-3]):[0-5]\\d$", RegexOptions.Compiled);

    // Not a real FHIRPath grammar check (that needs a real parser) - just the characters a
    // FHIRPath expression is ever built from, so obvious garbage (free text, stray punctuation)
    // is rejected without risking a false positive on a real expression this doesn't fully
    // understand.
    public static readonly Regex FhirPathCharacterPattern = new("^[\\w\\s.()\\[\\]'\"=!<>,:%$*/+&|-]+$", RegexOptions.Compiled);

    public static bool IsAbsoluteUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out _);

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

    // A basic sanity check, not a FHIRPath parser: rejects free text and unbalanced
    // parens/brackets/quotes, which is the class of mistake a facility typing this by hand
    // actually makes - it does not confirm the expression resolves to anything real.
    public static bool IsPlausibleFhirPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !FhirPathCharacterPattern.IsMatch(value))
        {
            return false;
        }
        return IsBalanced(value, '(', ')') && IsBalanced(value, '[', ']') && value.Count(c => c == '\'') % 2 == 0;
    }

    private static bool IsBalanced(string value, char open, char close)
    {
        var depth = 0;
        foreach (var c in value)
        {
            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
        }
        return depth == 0;
    }
}
