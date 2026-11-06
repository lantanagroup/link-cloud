using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class KafkaCatalogConsistencyTests
{
    /// <summary>
    /// These topics are in topics.txt with an error topic. Their retry or redrive
    /// topic is created from kafka-retry-services.txt, so topics.txt has no shared
    /// {topic}-Retry line. Cerner uses CernerPatientsAcquired-Retry-Census.
    /// Notification is redrive-only.
    /// </summary>
    private static readonly string[] SharedRetryNotListed =
    [
        "CernerPatientsAcquired",
        "NotificationRequested"
    ];

    [Fact]
    public void Catalog_MatchesTopicsFile_AndListenerSubscriptions()
    {
        var root = RepoRoot();
        var lines = File.ReadAllLines(Path.Combine(root, "topics.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        var names = lines.Select(line => line.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var retryServices = File.ReadAllLines(Path.Combine(root, "kafka-retry-services.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CernerPatientsAcquired:Census", retryServices);
        Assert.Contains("NotificationRequested:~Notification", retryServices);

        foreach (var entry in KafkaTopicCatalog.Topics)
        {
            Assert.Contains(entry.Topic, names);
            if (entry.Groups.Count == 0)
                continue;

            Assert.Contains(KafkaTopicCatalog.ErrorName(entry.Topic), names);
            if (SharedRetryNotListed.Contains(entry.Topic, StringComparer.Ordinal))
                Assert.DoesNotContain(KafkaTopicCatalog.RetryName(entry.Topic), names);
            else
                Assert.Contains(KafkaTopicCatalog.RetryName(entry.Topic), names);
        }

        foreach (var name in names)
        {
            var main = KafkaTopicCatalog.MainName(name);
            Assert.NotNull(KafkaTopicCatalog.Find(main));
        }

        var unresolved = new List<string>();
        foreach (var subscription in ListenerSubscriptions(root, unresolved))
        {
            Assert.NotNull(KafkaTopicCatalog.Find(subscription.Topic));
            Assert.Contains(subscription.Topic, names);
            var main = KafkaTopicCatalog.MainName(subscription.Topic);
            var groups = KafkaTopicCatalog.GroupsOf(main);
            Assert.Contains(subscription.Group, groups);
        }

        Assert.Empty(unresolved);
        Assert.Empty(TopicUsagesMissingFromCatalog(root, names));
    }

    private static IEnumerable<(string Topic, string Group)> ListenerSubscriptions(string root, List<string> unresolved)
    {
        var topicToken = new Regex(@"KafkaTopic\.(?<name>[A-Za-z0-9_]+)|nameof\(\s*KafkaTopic\.(?<name>[A-Za-z0-9_]+)\s*\)", RegexOptions.CultureInvariant);
        var groupPattern = new Regex(@"GroupId\s*=\s*(?<expr>[A-Za-z0-9_\.]+)", RegexOptions.CultureInvariant);
        var baseListener = new Regex(@":\s*BaseListener<(?<type>[A-Za-z0-9_]+)\s*,", RegexOptions.CultureInvariant);
        var javaTopic = new Regex(@"topics\s*=\s*Topics\.(?<name>[A-Z0-9_]+)", RegexOptions.CultureInvariant);
        var topicNames = KafkaTopicNames(Path.Combine(root, "DotNet", "Shared", "Application", "Models", "KafkaTopic.cs"));
        var javaConstants = JavaTopics(Path.Combine(root, "Java", "shared", "src", "main", "java", "com", "lantanagroup", "link", "shared", "kafka", "Topics.java"));

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories))
        {
            if (!file.Contains($"{Path.DirectorySeparatorChar}Listeners{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || IsGeneratedOrTest(file))
                continue;
            var text = File.ReadAllText(file);
            var topics = ConsumedTopics(text, topicToken, baseListener, topicNames);
            if (topics.Count == 0)
                continue;

            var group = groupPattern.Match(text);
            var groupName = group.Success ? ResolveGroup(root, file, group.Groups["expr"].Value) : null;
            if (groupName is null && text.Contains("CreateNotificationRequestedConsumer", StringComparison.Ordinal))
                groupName = ResolveConstantServiceName(root, "NotificationConstants");
            if (groupName is null)
            {
                unresolved.Add(Relative(root, file));
                continue;
            }

            foreach (var topic in topics)
                yield return (topic, groupName);
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Java"), "*.java", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            var text = File.ReadAllText(file);
            foreach (Match match in javaTopic.Matches(text))
            {
                if (!javaConstants.TryGetValue(match.Groups["name"].Value, out var topic))
                    continue;
                if (topic is "ResourcesNormalized" or "EvaluationRequested")
                {
                    yield return (topic, "measureeval");
                    yield return (topic, "measureeval-events");
                }
                else if (topic == "ReadyForValidation")
                {
                    yield return (topic, "validation");
                }
                else
                {
                    unresolved.Add(Relative(root, file) + " -> " + topic);
                }
            }
        }
    }

    private static string? ResolveGroup(string root, string file, string expression)
    {
        if (expression.EndsWith("Constants.ServiceName", StringComparison.Ordinal))
        {
            var typeName = expression[..expression.IndexOf(".ServiceName", StringComparison.Ordinal)];
            return ResolveConstantServiceName(root, typeName);
        }

        if (expression.EndsWith("ServiceConfigName", StringComparison.Ordinal)
            || expression.EndsWith("ServiceActivitySource.ServiceName", StringComparison.Ordinal))
            return ProjectServiceName(root, file);

        return null;
    }

    private static string? ProjectServiceName(string root, string file)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(file)!);
        while (dir is not null && !string.Equals(dir.FullName, root, StringComparison.OrdinalIgnoreCase))
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                var program = dir.GetFiles("Program.cs").FirstOrDefault();
                if (program is not null)
                {
                    var declared = Regex.Match(File.ReadAllText(program.FullName), @"(?<type>[A-Za-z0-9_]+)\.ServiceName");
                    if (declared.Success)
                    {
                        var resolved = ResolveConstantServiceName(root, declared.Groups["type"].Value);
                        if (resolved is not null)
                            return resolved;
                    }
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string? ResolveConstantServiceName(string root, string typeName)
    {
        var constant = Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories)
            .FirstOrDefault(candidate => !IsGeneratedOrTest(candidate) && Path.GetFileNameWithoutExtension(candidate) == typeName);
        if (constant is null)
            return null;
        var match = Regex.Match(File.ReadAllText(constant), @"ServiceName\s*=\s*""(?<name>[^""]+)""");
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static HashSet<string> ConsumedTopics(string text, Regex topicToken, Regex baseListener, Dictionary<string, string> topicNames)
    {
        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match subscribe in Regex.Matches(text, @"\.Subscribe\((?<args>[^;]*)\)", RegexOptions.Singleline))
        {
            var args = subscribe.Groups["args"].Value;
            AddTopicTokens(topics, topicToken, args, topicNames);
            if (Regex.IsMatch(args, @"\bTopicName\b"))
            {
                foreach (Match assigned in Regex.Matches(text, @"TopicName\s*=\s*nameof\(\s*KafkaTopic\.(?<name>[A-Za-z0-9_]+)\s*\)"))
                    topics.Add(topicNames.TryGetValue(assigned.Groups["name"].Value, out var value) ? value : assigned.Groups["name"].Value);
            }
        }

        foreach (Match match in baseListener.Matches(text))
            topics.Add(match.Groups["type"].Value);

        return topics;
    }

    private static void AddTopicTokens(HashSet<string> topics, Regex topicToken, string text, Dictionary<string, string> topicNames)
    {
        foreach (Match match in topicToken.Matches(text))
        {
            var raw = match.Groups["name"].Value;
            topics.Add(topicNames.TryGetValue(raw, out var value) ? value : raw);
        }
    }

    private static List<string> TopicUsagesMissingFromCatalog(string root, HashSet<string> topicFileNames)
    {
        var missing = new List<string>();
        var topicNames = KafkaTopicNames(Path.Combine(root, "DotNet", "Shared", "Application", "Models", "KafkaTopic.cs"));
        var token = new Regex(@"KafkaTopic\.(?<name>[A-Za-z0-9_]+)", RegexOptions.CultureInvariant);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedOrTest(file) || file.EndsWith($"{Path.DirectorySeparatorChar}KafkaTopic.cs", StringComparison.Ordinal))
                continue;
            foreach (Match match in token.Matches(File.ReadAllText(file)))
                NoteMissing(missing, topicFileNames, topicNames.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Groups["name"].Value, Relative(root, file));
        }

        var javaConstants = JavaTopics(Path.Combine(root, "Java", "shared", "src", "main", "java", "com", "lantanagroup", "link", "shared", "kafka", "Topics.java"));
        var javaToken = new Regex(@"Topics\.(?<name>[A-Z0-9_]+)", RegexOptions.CultureInvariant);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Java"), "*.java", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.EndsWith($"{Path.DirectorySeparatorChar}Topics.java", StringComparison.Ordinal))
                continue;
            foreach (Match match in javaToken.Matches(File.ReadAllText(file)))
            {
                if (javaConstants.TryGetValue(match.Groups["name"].Value, out var topic))
                    NoteMissing(missing, topicFileNames, topic, Relative(root, file));
            }
        }

        return missing;
    }

    private static void NoteMissing(List<string> missing, HashSet<string> topicFileNames, string topic, string file)
    {
        var main = KafkaTopicCatalog.MainName(topic);
        if (main.Length == 0 || KafkaTopicCatalog.Find(main) is not null)
            return;
        var description = file + " -> " + topic;
        if (!missing.Contains(description, StringComparer.Ordinal))
            missing.Add(description);
    }

    private static Dictionary<string, string> KafkaTopicNames(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        string? pending = null;
        foreach (var line in File.ReadAllLines(path))
        {
            var attribute = Regex.Match(line, @"StringValue\(""(?<value>[^""]+)""\)");
            if (attribute.Success)
            {
                pending = attribute.Groups["value"].Value;
                continue;
            }

            var member = Regex.Match(line, @"^\s*(?<name>[A-Za-z0-9_]+)\s*,?\s*$");
            if (!member.Success)
                continue;
            map[member.Groups["name"].Value] = pending ?? member.Groups["name"].Value;
            pending = null;
        }

        return map;
    }

    private static bool IsGeneratedOrTest(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}ServiceTests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || file.Contains($".Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> JavaTopics(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), @"String\s+(?<name>[A-Z0-9_]+)\s*=\s*""(?<value>[^""]+)"""))
            map[match.Groups["name"].Value] = match.Groups["value"].Value;
        return map;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")) && Directory.Exists(Path.Combine(dir.FullName, "DotNet")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("topics.txt was not found from the test output directory.");
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path);
}
