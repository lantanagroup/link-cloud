namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public readonly record struct KafkaBrowseAdmission(bool Allowed, string Reason, string Topic, string Main, string Kind);

/// <summary>
/// Which topic names a message browse may open. Catalog mains, their error, retry, and
/// redrive siblings, and a migration backup a stored record still names. Internal topics
/// and unknown names are refused.
/// </summary>
public static class KafkaBrowseAllowList
{
    public const string BackupPrefix = "_linkmig-";
    public const string KindMain = "main";
    public const string KindError = "error";
    public const string KindRetry = "retry";
    public const string KindRedrive = "redrive";
    public const string KindBackup = "backup";
    public const string KindOther = "other";

    public static KafkaBrowseAdmission Admit(string? topic, IReadOnlyCollection<string>? referencedBackups)
    {
        var name = (topic ?? "").Trim();
        if (name.Length == 0)
            return Refuse(name, "A topic name is required.");
        if (name.StartsWith("__", StringComparison.Ordinal))
            return Refuse(name, "Internal topics are not browsed.");
        if (name.StartsWith(BackupPrefix, StringComparison.Ordinal))
        {
            if (referencedBackups is not null && referencedBackups.Contains(name))
            {
                var main = BackupMain(name);
                return new KafkaBrowseAdmission(true, "", name, main, KindBackup);
            }

            return Refuse(name, "That backup is not referenced by a migration record.");
        }

        var entry = KafkaTopicCatalog.Find(name);
        if (entry is null)
            return Refuse(name, "That topic is not in the catalog.");

        var canonical = entry.Topic;
        if (IsExact(name, canonical))
            return Allow(canonical, canonical, KindMain);

        if (IsExact(name, canonical + "-Error"))
            return Allow(name, canonical, KindError);
        if (IsExact(name, canonical + "-Retry"))
            return Allow(name, canonical, KindRetry);

        var family = KafkaTopicCatalog.FamilyOf(canonical);
        if (TryService(name, canonical, "-Retry-", out var retryService))
        {
            if (Contains(family.RetryServices, retryService))
                return Allow(canonical + "-Retry-" + Match(family.RetryServices, retryService), canonical, KindRetry);
            return Refuse(name, "That retry topic is not a service on this family.");
        }

        if (TryService(name, canonical, "-Redrive-", out var redriveService))
        {
            var services = family.RetryServices.Concat(family.RedriveOnlyServices);
            if (Contains(services, redriveService))
                return Allow(canonical + "-Redrive-" + Match(services, redriveService), canonical, KindRedrive);
            return Refuse(name, "That redrive topic is not a service on this family.");
        }

        return Refuse(name, "That topic is not a pipeline topic or its error, retry, or redrive topic.");
    }

    public static IReadOnlyList<KafkaNamedTopic> MembersOf(string? topic)
    {
        var entry = KafkaTopicCatalog.Find(topic);
        var main = entry?.Topic ?? "";
        if (main.Length == 0)
            main = BackupMain(topic);
        if (main.Length == 0 || KafkaTopicCatalog.Find(main) is null)
            return [];

        var family = KafkaTopicCatalog.FamilyOf(main);
        var members = new List<KafkaNamedTopic>
        {
            Named(main, main, KindMain),
            Named(main + "-Error", main, KindError),
            Named(main + "-Retry", main, KindRetry)
        };
        foreach (var service in family.RetryServices)
        {
            members.Add(Named(main + "-Retry-" + service, main, KindRetry));
            members.Add(Named(main + "-Redrive-" + service, main, KindRedrive));
        }

        foreach (var service in family.RedriveOnlyServices)
            members.Add(Named(main + "-Redrive-" + service, main, KindRedrive));
        return members;
    }

    public static string BackupMain(string? topic)
    {
        var name = (topic ?? "").Trim();
        if (!name.StartsWith(BackupPrefix, StringComparison.Ordinal) || name.Length <= BackupPrefix.Length + 9)
            return "";
        var body = name[BackupPrefix.Length..];
        if (body.Length < 10 || body[^9] != '-')
            return "";
        var suffix = body[^8..];
        if (!suffix.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            return "";
        var main = body[..^9];
        return KafkaTopicCatalog.Find(main)?.Topic ?? "";
    }

    private static KafkaNamedTopic Named(string topic, string main, string kind) =>
        new() { Topic = topic, Main = main, Kind = kind, Browsable = true };

    private static KafkaBrowseAdmission Allow(string topic, string main, string kind) =>
        new(true, "", topic, main, kind);

    private static KafkaBrowseAdmission Refuse(string topic, string reason) =>
        new(false, reason, topic, "", "");

    private static bool IsExact(string name, string expected) =>
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryService(string name, string main, string marker, out string service)
    {
        service = "";
        var prefix = main + marker;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.Length == prefix.Length)
            return false;
        service = name[prefix.Length..];
        return service.Length > 0 && !service.Contains('/', StringComparison.Ordinal);
    }

    private static bool Contains(IEnumerable<string> services, string service) =>
        services.Any(item => string.Equals(item, service, StringComparison.OrdinalIgnoreCase));

    private static string Match(IEnumerable<string> services, string service) =>
        services.First(item => string.Equals(item, service, StringComparison.OrdinalIgnoreCase));
}
