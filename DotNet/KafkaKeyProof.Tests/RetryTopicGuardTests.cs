using System.Text.RegularExpressions;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class RetryTopicGuardTests
{
    [Fact]
    public void EveryRetryTargetHasAConsumerAndAMainTopic()
    {
        var root = FindRepoRoot();
        var constants = ReadServiceNames(root);
        var topics = ReadKafkaTopics(Path.Combine(root, "DotNet", "Shared", "Application", "Models", "KafkaTopic.cs"));
        var listed = ReadRetryServices(Path.Combine(root, "kafka-retry-services.txt"));
        var catalog = ReadTopicCatalog(Path.Combine(root, "topics.txt"));
        var hosted = ReadHostedRetries(root, constants, topics);
        var producers = ReadProducers(root, constants, topics);
        var problems = new List<string>();

        foreach (var pair in listed.Order(StringComparer.Ordinal))
        {
            if (!hosted.Contains(pair))
            {
                problems.Add("retry list has no hosted consumer: " + pair);
            }

            var main = pair.Split(':')[0];
            if (!catalog.Contains(main))
            {
                problems.Add("retry list topic is missing from topics.txt: " + main);
            }
        }

        foreach (var pair in hosted.Order(StringComparer.Ordinal))
        {
            if (!listed.Contains(pair))
            {
                problems.Add("hosted retry consumer is not in kafka-retry-services.txt: " + pair);
            }
        }

        foreach (var pair in producers.Order(StringComparer.Ordinal))
        {
            if (!listed.Contains(pair))
            {
                problems.Add("producer retry target is not in kafka-retry-services.txt: " + pair);
            }
        }

        Assert.Contains("CernerPatientsAcquired:Census", listed);
        Assert.Contains("CernerPatientsAcquired:Census", hosted);
        Assert.Contains("CernerPatientsAcquired:Census", producers);
        Assert.Contains("PayloadSubmitted:Report", listed);
        Assert.Contains("PayloadSubmitted", catalog);
        Assert.DoesNotContain("ReadyToAcquire:DataAcquisitionWorker", listed);
        Assert.DoesNotContain("ReadyToAcquire:DataAcquisitionWorker", producers);
        Assert.DoesNotContain("NotificationRequested:Notification", listed);
        Assert.DoesNotContain("NotificationRequested:Notification", producers);

        var yaml = File.ReadAllText(Path.Combine(root, "Azure_Pipelines", "kafka-topics-sync.yaml"));
        var script = File.ReadAllText(Path.Combine(root, "Scripts", "create-topics-rest.sh"));
        Assert.Contains("ERROR: kafka-retry-services.txt was not fetched", yaml);
        Assert.Contains("ERROR: Failed to create", yaml);
        Assert.DoesNotContain("WARNING: kafka-retry-services.txt was not fetched", yaml);
        Assert.Contains("ERROR: kafka-retry-services.txt was not found", script);
        Assert.Contains("ERROR: Failed to create", script);

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    private static HashSet<string> ReadRetryServices(string path)
    {
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var split = line.Split(':', 2);
            foreach (var service in split[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                pairs.Add(split[0] + ":" + service);
            }
        }

        return pairs;
    }

    private static HashSet<string> ReadTopicCatalog(string path)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            names.Add(line.Split(':')[0]);
        }

        return names;
    }

    private static Dictionary<string, string> ReadServiceNames(string root)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in EnumerateSource(root))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in ServiceNamePattern.Matches(text))
            {
                names[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return names;
    }

    private static Dictionary<string, string> ReadKafkaTopics(string path)
    {
        var topics = new Dictionary<string, string>(StringComparer.Ordinal);
        string? pending = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            var attribute = StringValuePattern.Match(line);
            if (attribute.Success)
            {
                pending = attribute.Groups[1].Value;
                continue;
            }

            var member = MemberPattern.Match(line);
            if (!member.Success)
            {
                continue;
            }

            topics[member.Groups[1].Value] = pending ?? member.Groups[1].Value;
            pending = null;
        }

        return topics;
    }

    private static HashSet<string> ReadHostedRetries(string root, Dictionary<string, string> constants, Dictionary<string, string> topics)
    {
        var hosted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in EnumerateSource(root))
        {
            if (!file.EndsWith("Program.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match block in RetrySettingsPattern.Matches(text))
            {
                var service = ResolveSettingsService(text, block.Groups["service"].Value, constants);
                foreach (Match topic in TopicValuePattern.Matches(block.Groups["topics"].Value))
                {
                    hosted.Add(MainTopic(topics, topic.Groups[1].Value) + ":" + service);
                }
            }
        }

        return hosted;
    }

    private static HashSet<string> ReadProducers(string root, Dictionary<string, string> constants, Dictionary<string, string> topics)
    {
        var producers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in EnumerateSource(root))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("RetryFailures => false", StringComparison.Ordinal))
            {
                continue;
            }

            string? service = null;
            foreach (Match subclass in BaseListenerPattern.Matches(text))
            {
                service ??= ServiceFor(file, root, constants);
                producers.Add(subclass.Groups[1].Value + ":" + service);
            }

            foreach (Match assignment in RetryAssignmentPattern.Matches(text))
            {
                var expression = assignment.Groups["expr"].Value;
                if (expression.Contains("rawmessage", StringComparison.Ordinal))
                {
                    if (!expression.Contains("KafkaTopicNames.Main(", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Dynamic retry topic is not reduced to the main topic in " + file + ": " + expression);
                    }

                    continue;
                }

                if (file.EndsWith("BaseListener.cs", StringComparison.Ordinal) && expression.Contains("this.TopicName", StringComparison.Ordinal))
                {
                    continue;
                }

                var main = ResolveProducerTopic(expression, text, topics);
                if (main == null)
                {
                    throw new InvalidOperationException("Could not resolve retry topic in " + file + ": " + expression);
                }

                service ??= ServiceFor(file, root, constants);
                producers.Add(main + ":" + service);
            }
        }

        return producers;
    }

    private static string? ResolveProducerTopic(string expression, string fileText, Dictionary<string, string> topics)
    {
        var named = NameofTopicPattern.Match(expression);
        if (named.Success)
        {
            return MainTopic(topics, named.Groups[1].Value);
        }

        var value = TopicValuePattern.Match(expression);
        if (value.Success)
        {
            return MainTopic(topics, value.Groups[1].Value);
        }

        if (expression.Contains("TopicName", StringComparison.Ordinal))
        {
            var local = LocalTopicNamePattern.Match(fileText);
            if (local.Success)
            {
                return MainTopic(topics, local.Groups[1].Value);
            }
        }

        return null;
    }

    private static string MainTopic(Dictionary<string, string> topics, string member)
    {
        if (!topics.TryGetValue(member, out var name))
        {
            throw new InvalidOperationException("Unknown KafkaTopic member " + member);
        }

        const string suffix = "-Retry";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    private static string ResolveSettingsService(string programText, string serviceExpression, Dictionary<string, string> constants)
    {
        var direct = ConstantsServicePattern.Match(serviceExpression);
        if (direct.Success && constants.TryGetValue(direct.Groups[1].Value, out var fromExpression))
        {
            return fromExpression;
        }

        var setup = SetupServicePattern.Match(programText);
        if (setup.Success && constants.TryGetValue(setup.Groups[1].Value, out var fromSetup))
        {
            return fromSetup;
        }

        throw new InvalidOperationException("Could not resolve retry service from " + serviceExpression);
    }

    private static string ServiceFor(string file, string root, Dictionary<string, string> constants)
    {
        var dir = Path.GetDirectoryName(file);
        while (dir != null && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var program = Path.Combine(dir, "Program.cs");
            if (File.Exists(program))
            {
                var setup = SetupServicePattern.Match(File.ReadAllText(program));
                if (setup.Success && constants.TryGetValue(setup.Groups[1].Value, out var name))
                {
                    return name;
                }
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir)
            {
                break;
            }

            dir = parent;
        }

        throw new InvalidOperationException("Could not resolve the service for " + file);
    }

    private static IEnumerable<string> EnumerateSource(string root)
    {
        return Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsSkipped(path));
    }

    private static bool IsSkipped(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is "bin" or "obj" or "ServiceTests" || part.EndsWith(".Tests", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")) && File.Exists(Path.Combine(dir.FullName, "kafka-retry-services.txt")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private static readonly Regex ServiceNamePattern = new(
        @"class\s+(\w+)[^{]*\{[^}]*?const string ServiceName = ""([^""]+)""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex StringValuePattern = new(@"\[StringValue\(""([^""]+)""\)\]", RegexOptions.CultureInvariant);

    private static readonly Regex MemberPattern = new(@"^(\w+),?$", RegexOptions.CultureInvariant);

    private static readonly Regex RetrySettingsPattern = new(
        @"new RetryListenerSettings\((?<service>.*?),\s*\[(?<topics>.*?)\]\s*\)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex TopicValuePattern = new(@"KafkaTopic\.(\w+)\.GetStringValue\(\)", RegexOptions.CultureInvariant);

    private static readonly Regex RetryAssignmentPattern = new(
        @"[Tt]ransientExceptionHandler\.Topic\s*=\s*(?<expr>[^;]+);",
        RegexOptions.CultureInvariant);

    private static readonly Regex BaseListenerPattern = new(@"class\s+\w+\s*:\s*BaseListener<(\w+)", RegexOptions.CultureInvariant);

    private static readonly Regex NameofTopicPattern = new(@"nameof\(KafkaTopic\.(\w+)\)", RegexOptions.CultureInvariant);

    private static readonly Regex LocalTopicNamePattern = new(@"const string TopicName = nameof\(KafkaTopic\.(\w+)\)", RegexOptions.CultureInvariant);

    private static readonly Regex ConstantsServicePattern = new(@"(\w+Constants)\.ServiceName", RegexOptions.CultureInvariant);

    private static readonly Regex SetupServicePattern = new(@"(?:SetupServiceInformation|RegisterAll)\(\s*(\w+)\.ServiceName", RegexOptions.CultureInvariant);
}
