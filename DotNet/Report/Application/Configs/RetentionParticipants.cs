using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.Options;

/// <summary>
/// The services expected to reply to every retention cleanup, read from <c>Retention:Participants</c> once at
/// startup. An invalid list yields no participants, so a cleanup is recorded <c>Failed</c> rather than sent to
/// a partial list. See dev-docs/retention-cleanup-record.md.
/// </summary>
public sealed class RetentionParticipants
{
    /// <summary>
    /// The configuration key that lists the participants.
    /// </summary>
    public const string Key = "Retention:Participants";

    /// <summary>
    /// The longest service name the cleanup record can store.
    /// </summary>
    public const int MaxServiceNameLength = 100;

    private RetentionParticipants(IReadOnlyList<RetentionParticipant> participants,
                                  IReadOnlyList<string> errors,
                                  bool isDefault)
    {
        Participants = participants;
        Errors = errors;
        IsDefault = isDefault;
    }

    /// <summary>
    /// The list used when <c>Retention:Participants</c> is not set: Report, MeasureEval, Validation and
    /// DataAcquisition, all report-scoped.
    /// </summary>
    public static IReadOnlyList<RetentionParticipant> Default { get; } =
    [
        new("Report", RetentionParticipantScope.Report),
        new("MeasureEval", RetentionParticipantScope.Report),
        new("Validation", RetentionParticipantScope.Report),
        new("DataAcquisition", RetentionParticipantScope.Report)
    ];

    /// <summary>
    /// The participants, in configuration order. Empty when the list is invalid.
    /// </summary>
    public IReadOnlyList<RetentionParticipant> Participants { get; }

    /// <summary>
    /// Why the list is invalid, one message per problem. Empty when it is valid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Whether the list is usable: at least one participant and no errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Whether the list is <see cref="Default"/> because <c>Retention:Participants</c> is not set.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Reads and validates <c>Retention:Participants</c>. An entry needs a <c>ServiceName</c> and a
    /// <c>Scope</c> of <c>Report</c> or <c>Cutoff</c>; names must be unique, ignoring case, and at least one
    /// participant must be report-scoped.
    /// </summary>
    public static RetentionParticipants FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Qualified so extract_config_keys.py can resolve it: several settings classes declare a bare Key.
        var section = configuration.GetSection(RetentionParticipants.Key);
        if (!IsSet(configuration, section))
        {
            return new RetentionParticipants(Default, [], isDefault: true);
        }

        var entries = section.GetChildren()
            .OrderBy(e => int.TryParse(e.Key, out var index) ? index : int.MaxValue)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        if (entries.Count == 0)
        {
            return Invalid([$"{Key} is set but lists no participants."]);
        }

        var participants = new List<RetentionParticipant>();
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var participant = ReadEntry(entry, names, errors);
            if (participant is not null)
            {
                participants.Add(participant);
            }
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        // Without a report-scoped participant nothing deletes per-report data, yet every report would count
        // as ready to scrub. Report always deletes its own, so such a list is a misconfiguration.
        if (participants.All(p => p.Scope != RetentionParticipantScope.Report))
        {
            return Invalid([$"{Key} has no participant with Scope Report."]);
        }

        return new RetentionParticipants(participants, [], isDefault: false);
    }

    // An empty JSON array is stored as the key with a null value, which Exists() reports as absent. It still
    // shows among its parent's children, and must read as an empty list rather than "not set".
    private static bool IsSet(IConfiguration configuration, IConfigurationSection section)
    {
        if (section.Exists())
        {
            return true;
        }

        var parentKey = ConfigurationPath.GetParentPath(Key)!;
        return configuration.GetSection(parentKey)
            .GetChildren()
            .Any(c => string.Equals(c.Key, section.Key, StringComparison.OrdinalIgnoreCase));
    }

    private static RetentionParticipants Invalid(IReadOnlyList<string> errors)
    {
        return new RetentionParticipants([], errors, isDefault: false);
    }

    private static RetentionParticipant? ReadEntry(IConfigurationSection entry,
                                                   HashSet<string> names,
                                                   List<string> errors)
    {
        var path = $"{Key}:{entry.Key}";
        var name = entry["ServiceName"]?.Trim();
        var scopeText = entry["Scope"]?.Trim();
        var errorCount = errors.Count;

        if (string.IsNullOrEmpty(name))
        {
            errors.Add($"{path} has no ServiceName.");
        }
        else if (name.Length > MaxServiceNameLength)
        {
            errors.Add($"{path}:ServiceName is longer than {MaxServiceNameLength} characters.");
        }
        else if (!names.Add(name))
        {
            errors.Add($"{path}:ServiceName '{name}' is listed more than once.");
        }

        var scope = ParseScope(scopeText);
        if (string.IsNullOrEmpty(scopeText))
        {
            errors.Add($"{path} has no Scope.");
        }
        else if (scope is null)
        {
            errors.Add($"{path}:Scope '{scopeText}' is not one of " +
                       $"{string.Join(", ", Enum.GetNames<RetentionParticipantScope>())}.");
        }

        if (errors.Count > errorCount)
        {
            return null;
        }

        return new RetentionParticipant(name!, scope!.Value);
    }

    // Matches the names only: Enum.TryParse would also accept a number such as "7".
    private static RetentionParticipantScope? ParseScope(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var match = Enum.GetNames<RetentionParticipantScope>()
            .FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : Enum.Parse<RetentionParticipantScope>(match);
    }
}
