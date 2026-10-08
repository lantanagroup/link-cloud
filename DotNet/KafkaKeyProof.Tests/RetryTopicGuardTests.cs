using System.Diagnostics;
using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class RetryTopicGuardTests
{
    [Fact]
    public void EveryRetryTargetHasAConsumerAndAMainTopic()
    {
        var root = FindRepoRoot();
        var constants = ReadServiceNames(root);
        var topics = ReadKafkaTopics(Path.Combine(root, "DotNet", "Shared", "Application", "Models", "KafkaTopic.cs"));
        var list = ReadRetryServices(Path.Combine(root, "kafka-retry-services.txt"));
        var catalog = ReadTopicCatalog(Path.Combine(root, "topics.txt"));
        var hosted = ReadHostedRetries(root, constants, topics);
        var producers = ReadProducers(root, constants, topics);
        var problems = new List<string>();

        foreach (var pair in list.Retry.Order(StringComparer.Ordinal))
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

        foreach (var pair in list.RedriveOnly.Order(StringComparer.Ordinal))
        {
            if (hosted.Contains(pair) || producers.Contains(pair))
            {
                problems.Add("redrive-only row also retries: " + pair);
            }

            var main = pair.Split(':')[0];
            if (!catalog.Contains(main))
            {
                problems.Add("redrive-only topic is missing from topics.txt: " + main);
            }
        }

        foreach (var pair in hosted.Order(StringComparer.Ordinal))
        {
            if (!list.Retry.Contains(pair))
            {
                problems.Add("hosted retry consumer is not in kafka-retry-services.txt: " + pair);
            }
        }

        foreach (var pair in producers.Order(StringComparer.Ordinal))
        {
            if (!list.Retry.Contains(pair))
            {
                problems.Add("producer retry target is not in kafka-retry-services.txt: " + pair);
            }
        }

        Assert.Contains("CernerPatientsAcquired:Census", list.Retry);
        Assert.Contains("CernerPatientsAcquired:Census", hosted);
        Assert.Contains("CernerPatientsAcquired:Census", producers);
        Assert.Contains("PayloadSubmitted:Report", list.Retry);
        Assert.Contains("PayloadSubmitted", catalog);
        Assert.Contains("ReadyToAcquire:DataAcquisitionWorker", list.RedriveOnly);
        Assert.Contains("NotificationRequested:Notification", list.RedriveOnly);
        Assert.DoesNotContain("ReadyToAcquire:DataAcquisitionWorker", list.Retry);
        Assert.DoesNotContain("ReadyToAcquire:DataAcquisitionWorker", producers);
        Assert.DoesNotContain("NotificationRequested:Notification", list.Retry);
        Assert.DoesNotContain("NotificationRequested:Notification", producers);

        var yaml = File.ReadAllText(Path.Combine(root, "Azure_Pipelines", "kafka-topics-sync.yaml"));
        var script = File.ReadAllText(Path.Combine(root, "Scripts", "create-topics-rest.sh"));
        Assert.Contains("ERROR: kafka-retry-services.txt was not fetched", yaml);
        Assert.Contains("ERROR: Failed to create", yaml);
        Assert.DoesNotContain("WARNING: kafka-retry-services.txt was not fetched", yaml);
        Assert.Contains("ERROR: kafka-retry-services.txt was not found", script);
        Assert.Contains("ERROR: Failed to create", script);
        const string strip = "SERVICE=\"${SERVICE#\"~\"}\"";
        Assert.Contains(strip, yaml);
        Assert.Contains(strip, script);
        Assert.DoesNotContain("SERVICE=\"${SERVICE#~}\"", yaml);
        Assert.DoesNotContain("SERVICE=\"${SERVICE#~}\"", script);
        Assert.Contains("SUFFIXES=(Redrive)", yaml);
        Assert.Contains("SUFFIXES=(Redrive)", script);
        Assert.Contains("for SUFFIX in \"${SUFFIXES[@]}\"; do", yaml);
        Assert.Contains("for SUFFIX in \"${SUFFIXES[@]}\"; do", script);

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void EverySubscribedTopicIsCreated()
    {
        var root = FindRepoRoot();
        var constants = ReadServiceNames(root);
        var topics = ReadKafkaTopics(Path.Combine(root, "DotNet", "Shared", "Application", "Models", "KafkaTopic.cs"));
        var list = ReadRetryServices(Path.Combine(root, "kafka-retry-services.txt"));
        var catalog = ReadTopicCatalog(Path.Combine(root, "topics.txt"));
        var hosted = ReadHostedRetries(root, constants, topics);
        var subscribed = ReadSubscribedTopics(root, constants, topics, hosted);
        var problems = new List<string>();

        foreach (var topic in subscribed.Order(StringComparer.Ordinal))
        {
            if (!IsCreated(topic, catalog, list))
            {
                problems.Add("subscribed topic is not created: " + topic);
            }
        }

        foreach (var pair in list.RedriveOnly)
        {
            var parts = pair.Split(':');
            var redrive = KafkaTopicNames.Redrive(parts[0], parts[1]);
            if (!subscribed.Contains(redrive))
            {
                problems.Add("redrive-only topic has no subscriber: " + redrive);
            }
        }

        Assert.Contains("NotificationRequested-Redrive-Notification", subscribed);
        Assert.Contains("ReadyToAcquire-Redrive-DataAcquisitionWorker", subscribed);
        Assert.DoesNotContain("NotificationRequested-Retry-Notification", SubscribedRetryTopics(list));
        Assert.DoesNotContain("ReadyToAcquire-Retry-DataAcquisitionWorker", SubscribedRetryTopics(list));
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void BashParsesRedriveOnlyRowsAndRejectsATildeInsideAName()
    {
        var root = FindRepoRoot();
        var yaml = ExtractRetryParse(File.ReadAllText(Path.Combine(root, "Azure_Pipelines", "kafka-topics-sync.yaml")));
        var script = ExtractRetryParse(File.ReadAllText(Path.Combine(root, "Scripts", "create-topics-rest.sh")));
        Assert.Equal(script, yaml);
        Assert.Contains("SERVICE=\"${SERVICE#\"~\"}\"", script);

        var bash = FindBash();
        var list = Path.Combine(root, "kafka-retry-services.txt");
        var withHome = RunRetryParse(bash, script, list, keepHome: true);
        var withoutHome = RunRetryParse(bash, script, list, keepHome: false);
        Assert.True(withHome.Exit == 0, withHome.Error);
        Assert.True(withoutHome.Exit == 0, withoutHome.Error);
        Assert.Equal(withHome.Topics, withoutHome.Topics);
        Assert.Contains("NotificationRequested-Redrive-Notification", withHome.Topics);
        Assert.Contains("ReadyToAcquire-Redrive-DataAcquisitionWorker", withHome.Topics);
        Assert.DoesNotContain("NotificationRequested-Retry-Notification", withHome.Topics);
        Assert.DoesNotContain("ReadyToAcquire-Retry-DataAcquisitionWorker", withHome.Topics);
        Assert.Contains("AuditableEventOccurred-Retry-Audit", withHome.Topics);
        Assert.Contains("AuditableEventOccurred-Redrive-Audit", withHome.Topics);
        Assert.Contains("PatientEvent-Retry-Report", withHome.Topics);
        Assert.Contains("PatientEvent-Redrive-QueryDispatch", withHome.Topics);
        Assert.Equal(34, withHome.Topics.Count);

        var empty = RunRetryParse(bash, script, WriteList("X:~"), keepHome: true);
        var embedded = RunRetryParse(bash, script, WriteList("X:a~b"), keepHome: true);
        Assert.NotEqual(0, empty.Exit);
        Assert.NotEqual(0, embedded.Exit);
        Assert.Empty(empty.Topics);
        Assert.Empty(embedded.Topics);
    }

    private static string ExtractRetryParse(string text)
    {
        const string begin = "# retry-service-parse: begin";
        const string end = "# retry-service-parse: end";
        var start = text.IndexOf(begin, StringComparison.Ordinal);
        var stop = text.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || stop < start)
        {
            throw new InvalidOperationException("Retry service parse block was not found.");
        }

        var lines = text[(start + begin.Length)..stop]
            .Split('\n')
            .Select(line => line.Trim().TrimEnd('\r'))
            .Where(line => line.Length > 0);
        return string.Join("\n", lines);
    }

    private static string WriteList(string line)
    {
        var path = Path.Combine(Path.GetTempPath(), "kafka-retry-parse-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, line + "\n");
        return path;
    }

    private static (int Exit, HashSet<string> Topics, string Error) RunRetryParse(string bash, string parseBlock, string listPath, bool keepHome)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "kafka-retry-parse-" + Guid.NewGuid().ToString("N") + ".sh");
        var script = "#!/usr/bin/env bash\nset -euo pipefail\n" +
            (keepHome ? "" : "unset HOME\n") +
            "while IFS=: read -r MAIN_TOPIC SERVICES || [[ -n \"${MAIN_TOPIC:-}\" ]]; do\n" +
            "  [[ -z \"${MAIN_TOPIC:-}\" || \"$MAIN_TOPIC\" =~ ^# ]] && continue\n" +
            "  IFS=',' read -ra SERVICE_LIST <<< \"$SERVICES\"\n" +
            "  for SERVICE in \"${SERVICE_LIST[@]}\"; do\n" +
            parseBlock + "\n" +
            "    for SUFFIX in \"${SUFFIXES[@]}\"; do\n" +
            "      printf '%s\\n' \"${MAIN_TOPIC}-${SUFFIX}-${SERVICE}\"\n" +
            "    done\n" +
            "  done\n" +
            "done < \"$1\"\n";
        File.WriteAllText(scriptPath, script.Replace("\r\n", "\n", StringComparison.Ordinal));
        try
        {
            var start = new ProcessStartInfo(bash)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--noprofile");
            start.ArgumentList.Add("--norc");
            start.ArgumentList.Add(scriptPath.Replace('\\', '/'));
            start.ArgumentList.Add(listPath.Replace('\\', '/'));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("bash parse did not finish.");
            }

            var topics = new HashSet<string>(
                stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.Ordinal);
            return (process.ExitCode, topics, stderr);
        }
        finally
        {
            File.Delete(scriptPath);
            if (listPath.Contains("kafka-retry-parse-", StringComparison.Ordinal))
            {
                File.Delete(listPath);
            }
        }
    }

    private static string FindBash()
    {
        var candidates = new List<string>();
        var git = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin", "bash.exe");
        if (File.Exists(git))
        {
            candidates.Add(git);
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "bash.exe" : "bash");
            if (File.Exists(candidate) && !candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(candidate);
            }
        }

        foreach (var candidate in candidates)
        {
            if (BashPrintsOk(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("bash was not found.");
    }

    private static bool BashPrintsOk(string bash)
    {
        try
        {
            var start = new ProcessStartInfo(bash)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--noprofile");
            start.ArgumentList.Add("--norc");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("echo ok");
            using var process = Process.Start(start);
            if (process == null)
            {
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0 && stdout.Contains("ok", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static HashSet<string> SubscribedRetryTopics(RetryList list)
    {
        var created = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in list.Retry)
        {
            var parts = pair.Split(':');
            created.Add(KafkaTopicNames.Retry(parts[0], parts[1]));
        }

        return created;
    }

    private static bool IsCreated(string topic, HashSet<string> catalog, RetryList list)
    {
        if (catalog.Contains(topic))
        {
            return true;
        }

        if (KafkaTopicNames.TryMainFromRetry(topic, out var main, out var service))
        {
            return list.Retry.Contains(main + ":" + service);
        }

        if (KafkaTopicNames.TryMainFromRedrive(topic, out main, out service))
        {
            var pair = main + ":" + service;
            return list.Retry.Contains(pair) || list.RedriveOnly.Contains(pair);
        }

        return false;
    }

    private sealed class RetryList
    {
        public HashSet<string> Retry { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RedriveOnly { get; } = new(StringComparer.Ordinal);
    }

    private static RetryList ReadRetryServices(string path)
    {
        var list = new RetryList();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var split = line.Split(':', 2);
            if (split.Length != 2 || split[0].Length == 0)
            {
                throw new InvalidOperationException("Invalid retry-services row: " + line);
            }

            foreach (var service in split[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var name = service;
                var redriveOnly = name.StartsWith('~');
                if (redriveOnly)
                {
                    name = name[1..];
                }

                if (name.Length == 0 || name.Contains('~') || name.Contains(':'))
                {
                    throw new InvalidOperationException("Invalid service in retry-services row: " + line);
                }

                var pair = split[0] + ":" + name;
                var target = redriveOnly ? list.RedriveOnly : list.Retry;
                var other = redriveOnly ? list.Retry : list.RedriveOnly;
                if (!target.Add(pair) || other.Contains(pair))
                {
                    throw new InvalidOperationException("Duplicate retry-services row: " + pair);
                }
            }
        }

        return list;
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

    private static HashSet<string> ReadSubscribedTopics(
        string root,
        Dictionary<string, string> constants,
        Dictionary<string, string> topics,
        HashSet<string> hosted)
    {
        var subscribed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in hosted)
        {
            var parts = pair.Split(':');
            subscribed.Add(KafkaTopicNames.Retry(parts[0], parts[1]));
        }

        foreach (var file in EnumerateSource(root))
        {
            var text = File.ReadAllText(file);
            foreach (Match subclass in BaseListenerPattern.Matches(text))
            {
                var service = ServiceFor(file, root, constants);
                foreach (var topic in KafkaTopicNames.Subscription(subclass.Groups[1].Value, service))
                {
                    subscribed.Add(topic);
                }
            }

            var index = 0;
            while ((index = text.IndexOf(".Subscribe(", index, StringComparison.Ordinal)) >= 0)
            {
                var open = index + ".Subscribe".Length;
                var inner = ReadCall(text, open);
                var argument = inner.Trim();
                if (argument.StartsWith("KafkaTopicNames.Subscription(", StringComparison.Ordinal))
                {
                    if (!file.EndsWith("BaseListener.cs", StringComparison.Ordinal))
                    {
                        var call = argument["KafkaTopicNames.Subscription".Length..];
                        var subscription = ReadCall(call, 0);
                        var args = SplitArgs(subscription);
                        if (args.Count != 2)
                        {
                            throw new InvalidOperationException("Subscription needs two arguments in " + file);
                        }

                        var main = ResolveSubscribedTopic(args[0], text, topics);
                        var service = ResolveSubscribedService(args[1], constants);
                        foreach (var topic in KafkaTopicNames.Subscription(main, service))
                        {
                            subscribed.Add(topic);
                        }
                    }
                }
                else if (argument == "topics" && file.EndsWith("RetryListener.cs", StringComparison.Ordinal))
                {
                    // The hosted retry set above is the topic list this listener subscribes to.
                }
                else if (argument == "topics" && file.EndsWith("KafkaConsumerService.cs", StringComparison.Ordinal))
                {
                    AddAdminConsumerTopics(root, subscribed);
                }
                else if (argument == "topics" && file.EndsWith("KafkaErrorMonitor.cs", StringComparison.Ordinal))
                {
                    if (!text.Contains("DiscoverErrorAndRetryTopicsAsync(", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("KafkaErrorMonitor subscribes to a list that is not discovered from the broker.");
                    }
                }
                else if (argument == "memberTopic"
                    && text.Contains("const string memberTopic = \"ops-proof-members\"", StringComparison.Ordinal)
                    && file.Replace('/', '\\').EndsWith("\\KafkaOps.Proof\\KafkaOpsConsoleFlowTests.cs", StringComparison.Ordinal))
                {
                    // This proof subscribes throwaway readers to ops-proof-members, a topic the same test creates.
                    // That name is not a pipeline subscription and does not belong in topics.txt.
                }
                else
                {
                    throw new InvalidOperationException("Unresolved Subscribe in " + file + ": " + argument);
                }

                index = open + inner.Length + 2;
            }
        }

        AddJavaListenerTopics(root, subscribed);
        return subscribed;
    }

    private static void AddAdminConsumerTopics(string root, HashSet<string> subscribed)
    {
        var path = Path.Combine(root, "DotNet", "Admin.BFF", "Application", "Commands", "Integration", "KafkaConsumerManager.cs");
        var text = File.ReadAllText(path);
        var suffix = Regex.Match(text, "errorTopic = \"([^\"]+)\"").Groups[1].Value;
        if (suffix.Length == 0)
        {
            throw new InvalidOperationException("Admin consumer error suffix was not found.");
        }

        foreach (Match match in Regex.Matches(text, @"KafkaTopic\.(\w+)\.ToString\(\)"))
        {
            var name = match.Groups[1].Value;
            var tail = text[(match.Index + match.Length)..];
            if (tail.TrimStart().StartsWith("+ errorTopic", StringComparison.Ordinal))
            {
                subscribed.Add(name + suffix);
            }
            else
            {
                subscribed.Add(name);
            }
        }
    }

    private static void AddJavaListenerTopics(string root, HashSet<string> subscribed)
    {
        var topics = new Dictionary<string, string>(StringComparer.Ordinal);
        var topicsFile = Path.Combine(root, "Java", "shared", "src", "main", "java", "com", "lantanagroup", "link", "shared", "kafka", "Topics.java");
        foreach (Match match in Regex.Matches(File.ReadAllText(topicsFile), "public static final String (\\w+) = \"([^\"]+)\";"))
        {
            topics[match.Groups[1].Value] = match.Groups[2].Value;
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Java"), "*.java", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.AltDirectorySeparatorChar}src{Path.AltDirectorySeparatorChar}test{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var search = 0;
            while ((search = text.IndexOf("@KafkaListener(", search, StringComparison.Ordinal)) >= 0)
            {
                var open = text.IndexOf('(', search);
                var body = ReadCall(text, open);
                AddTopicReferences(body, topics, subscribed, file);
                search = open + body.Length + 2;
            }

            search = 0;
            while ((search = text.IndexOf(".includeTopics(", search, StringComparison.Ordinal)) >= 0)
            {
                var open = text.IndexOf('(', search);
                var body = ReadCall(text, open);
                var create = text.IndexOf(".create(", open, StringComparison.Ordinal);
                var block = create > open ? text[open..create] : text[open..];
                var suffix = Regex.Match(block, "retryTopicSuffix\\(\"([^\"]+)\"\\)");
                if (!suffix.Success)
                {
                    throw new InvalidOperationException("includeTopics has no retry suffix in " + file);
                }

                foreach (Match reference in Regex.Matches(body, @"Topics\.(\w+)"))
                {
                    if (!topics.TryGetValue(reference.Groups[1].Value, out var name))
                    {
                        throw new InvalidOperationException("Unknown topic " + reference.Value + " in " + file);
                    }

                    subscribed.Add(name);
                    subscribed.Add(name + suffix.Groups[1].Value);
                }

                search = open + body.Length + 2;
            }
        }
    }

    private static void AddTopicReferences(string body, Dictionary<string, string> topics, HashSet<string> subscribed, string file)
    {
        var assignment = Regex.Match(body, @"topics\s*=\s*(\{[^}]*\}|Topics\.\w+|""[^""]+"")");
        if (!assignment.Success)
        {
            throw new InvalidOperationException("Kafka listener topic was not resolved in " + file);
        }

        var value = assignment.Groups[1].Value;
        if (value.Length >= 2 && value[0] == '"')
        {
            subscribed.Add(value[1..^1]);
            return;
        }

        var found = false;
        foreach (Match reference in Regex.Matches(value, @"Topics\.(\w+)"))
        {
            if (!topics.TryGetValue(reference.Groups[1].Value, out var name))
            {
                throw new InvalidOperationException("Unknown topic " + reference.Value + " in " + file);
            }

            subscribed.Add(name);
            found = true;
        }

        if (!found)
        {
            throw new InvalidOperationException("Kafka listener topic was not resolved in " + file);
        }
    }

    private static string ResolveSubscribedTopic(string argument, string fileText, Dictionary<string, string> topics)
    {
        var named = NameofTopicPattern.Match(argument);
        if (named.Success && argument == named.Value)
        {
            return RequireTopicMember(topics, named.Groups[1].Value);
        }

        var asString = Regex.Match(argument, @"^KafkaTopic\.(\w+)\.ToString\(\)$");
        if (asString.Success)
        {
            return RequireTopicMember(topics, asString.Groups[1].Value);
        }

        if (argument == "TopicName")
        {
            var local = LocalTopicNamePattern.Match(fileText);
            if (local.Success)
            {
                return RequireTopicMember(topics, local.Groups[1].Value);
            }
        }

        throw new InvalidOperationException("Could not resolve subscribed topic: " + argument);
    }

    private static string RequireTopicMember(Dictionary<string, string> topics, string member)
    {
        if (!topics.ContainsKey(member))
        {
            throw new InvalidOperationException("Unknown KafkaTopic member " + member);
        }

        return member;
    }

    private static string ResolveSubscribedService(string argument, Dictionary<string, string> constants)
    {
        if (argument.Length >= 2 && argument[0] == '"' && argument[^1] == '"')
        {
            return argument[1..^1];
        }

        var named = ConstantsServicePattern.Match(argument);
        if (named.Success && argument == named.Value && constants.TryGetValue(named.Groups[1].Value, out var service))
        {
            return service;
        }

        throw new InvalidOperationException("Could not resolve subscribed service: " + argument);
    }

    private static string ReadCall(string text, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return text[(openParen + 1)..i];
                }
            }
        }

        throw new InvalidOperationException("Unbalanced call.");
    }

    private static List<string> SplitArgs(string inner)
    {
        var args = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '(')
            {
                depth++;
            }
            else if (inner[i] == ')')
            {
                depth--;
            }
            else if (inner[i] == ',' && depth == 0)
            {
                args.Add(inner[start..i].Trim());
                start = i + 1;
            }
        }

        args.Add(inner[start..].Trim());
        return args;
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
