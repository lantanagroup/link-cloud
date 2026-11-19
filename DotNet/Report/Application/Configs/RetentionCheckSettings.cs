using System.Globalization;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Application.Options;

/// <summary>
/// How the scheduled retention check selects reports: the retention window and the most reports one cleanup
/// may hold. Read once at startup. See dev-docs/retention-check.md.
/// </summary>
/// <remarks>
/// An invalid value never falls back to a default. Retention deletes data, so a mistyped window must stop the
/// check rather than quietly choose another one.
/// </remarks>
public sealed class RetentionCheckSettings
{
    /// <summary>
    /// The configuration key for the retention window, an ISO-8601 duration such as <c>P3M</c>.
    /// </summary>
    public const string WindowKey = "Retention:Window";

    /// <summary>
    /// The configuration key for the most reports one cleanup may hold.
    /// </summary>
    public const string MaxReportsPerCleanupKey = "Retention:MaxReportsPerCleanup";

    /// <summary>
    /// The window used when <see cref="WindowKey"/> is not set: three months.
    /// </summary>
    public const string DefaultWindow = "P3M";

    /// <summary>
    /// The cap used when <see cref="MaxReportsPerCleanupKey"/> is not set.
    /// </summary>
    public const int DefaultMaxReportsPerCleanup = 25;

    /// <summary>
    /// The largest cap allowed. One cleanup is one Kafka message, at most about 350 bytes per report, so this
    /// keeps a request near 350 KB, well under Kafka's 1 MB default message size.
    /// </summary>
    public const int MaxAllowedReportsPerCleanup = 1000;

    private RetentionCheckSettings(string windowText,
                                   RetentionWindow? window,
                                   int maxReportsPerCleanup,
                                   IReadOnlyList<string> errors)
    {
        WindowText = windowText;
        Window = window;
        MaxReportsPerCleanup = maxReportsPerCleanup;
        Errors = errors;
    }

    /// <summary>
    /// The window as configured, or <see cref="DefaultWindow"/> when the key is not set.
    /// </summary>
    public string WindowText { get; }

    /// <summary>
    /// The window in months or days. Null when <see cref="WindowText"/> is invalid.
    /// </summary>
    public RetentionWindow? Window { get; }

    /// <summary>
    /// The most reports one cleanup may hold. Zero when the configured value is invalid.
    /// </summary>
    public int MaxReportsPerCleanup { get; }

    /// <summary>
    /// Why the settings are invalid, one message per problem. Empty when they are valid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Whether the retention check can run with these settings.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Reads and validates <see cref="WindowKey"/> and <see cref="MaxReportsPerCleanupKey"/>, using the
    /// defaults for any key that is not set.
    /// </summary>
    public static RetentionCheckSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<string>();

        // Qualified so extract_config_keys.py can resolve them.
        var windowText = configuration.GetSection(RetentionCheckSettings.WindowKey).Value ?? DefaultWindow;
        var capText = configuration.GetSection(RetentionCheckSettings.MaxReportsPerCleanupKey).Value;

        if (!RetentionWindowParser.TryParse(windowText, out var window, out var windowError))
        {
            errors.Add($"{WindowKey} {windowError}");
        }

        var cap = ReadCap(capText, errors);

        return new RetentionCheckSettings(windowText, window, cap, errors);
    }

    private static int ReadCap(string? text, List<string> errors)
    {
        if (text is null)
        {
            return DefaultMaxReportsPerCleanup;
        }

        // A cleanup the broker rejects as too large would leave its reports claimed but never sent.
        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var cap) ||
            cap < 1 ||
            cap > MaxAllowedReportsPerCleanup)
        {
            errors.Add($"{MaxReportsPerCleanupKey} '{text}' is not a whole number from 1 to " +
                       $"{MaxAllowedReportsPerCleanup}.");
            return 0;
        }

        return cap;
    }
}
