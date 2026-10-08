using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class CatalogGuardTests
{
    private static readonly string[] KnownDynamicGapFiles =
    [
        "DeadLetterExceptionHandler.cs",
        "RetryJob.cs",
        "TransientExceptionHandler.cs"
    ];

    [Fact]
    public void EveryProduceAndSubscribeSiteIsInTheCatalog()
    {
        var scan = Scan(FindRepoRoot());
        Assert.Equal(KnownDynamicGapFiles, scan.DynamicGapFiles);
        Assert.False(KafkaTopicCatalog.IsHardBlocked("DataAcquisitionRequested"));
        Assert.False(KafkaTopicCatalog.IsOrderSensitive("ReadyToAcquire"));
        Assert.False(KafkaTopicCatalog.IsOrderSensitive("ReadyForValidation"));
        Assert.Contains(scan.Producers, hit => hit.Topic == "PayloadSubmitted" && hit.Path == "DotNet/Submission/KafkaProducers/PayloadSubmittedProducer.cs");
        Assert.Contains(scan.Producers, hit => hit.Topic == "PatientEvent" && hit.Path == "DotNet/Census/Application/Services/EventProducerService.cs");
        Assert.Contains(scan.Producers, hit => hit.Topic == "DataAcquisitionRequested" && hit.Path.EndsWith("AbstractResourceConsumer.java", StringComparison.Ordinal));
        Assert.Contains(scan.Producers, hit => hit.Topic == "Service-Healthcheck" && hit.Path.EndsWith("KafkaHealthCheckIndicator.java", StringComparison.Ordinal));
        Assert.Contains(scan.Consumers, hit => hit.Topic == "AuditableEventOccurred" && hit.Path.EndsWith("AuditEventListener.cs", StringComparison.Ordinal));
        Assert.Contains(scan.Consumers, hit => hit.Topic == "SubmitPayload" && hit.Path.EndsWith("SubmitPayloadListener.cs", StringComparison.Ordinal));
        Assert.Contains(scan.Consumers, hit => hit.Topic == "ReadyToAcquire" && hit.Path.EndsWith("ReadyToAcquireListener.cs", StringComparison.Ordinal));
        Assert.Contains(scan.Consumers, hit => hit.Topic == "ResourcesNormalized" && hit.Path.EndsWith("ResourcesNormalizedConsumer.java", StringComparison.Ordinal));
        var manager = scan.Consumers.Single(hit => hit.Topic == "ReportScheduled" && hit.Path.EndsWith("KafkaConsumerManager.cs", StringComparison.Ordinal));
        Assert.True(manager.ControlPlane);
        var command = scan.Producers.Single(hit => hit.Path.EndsWith("CreatePatientEvent.cs", StringComparison.Ordinal));
        Assert.Equal("PatientEvent", command.Topic);
        Assert.True(command.ControlPlane);
        var problems = Compare(scan, dropTopic: null, dropPath: null);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void DroppingACatalogProducerPathFailsTheGuard()
    {
        var scan = Scan(FindRepoRoot());
        var hit = scan.Producers
            .OrderBy(site => site.Topic, StringComparer.Ordinal)
            .ThenBy(site => site.Path, StringComparer.Ordinal)
            .First(site => CatalogHas(site, producers: true));
        var problems = Compare(scan, hit.Topic, hit.Path);
        Assert.Contains(problems, problem => problem.Contains(hit.Path, StringComparison.Ordinal) && problem.Contains(hit.Topic, StringComparison.Ordinal));
        Assert.True(CatalogHas(hit, producers: true));
    }

    [Fact]
    public void ProduceOfAVariableBoundToAKafkaTopicConstantIsNotADynamicGap()
    {
        const string path = "DotNet/Synthetic/UnboundTopicProduce.cs";
        const string bound = """
            public class UnboundTopicProduce
            {
                public void Run()
                {
                    var topic = nameof(KafkaTopic.PatientEvent);
                    producer.Produce(topic, new Message<string, object>());
                }
            }
            """;
        var boundScan = new ScanResult();
        ReadCSharp(path, bound, boundScan);
        boundScan.Finish();
        Assert.Contains(boundScan.Producers, hit => hit.Topic == "PatientEvent" && string.Equals(hit.Path, path, StringComparison.Ordinal));
        Assert.DoesNotContain("UnboundTopicProduce.cs", boundScan.DynamicGapFiles);
        var missing = Compare(boundScan, dropTopic: null, dropPath: null);
        Assert.Contains(missing, problem => problem.Contains(path, StringComparison.Ordinal));

        const string unbound = """
            public class UnboundTopicProduce
            {
                public void Run(string topicVariable)
                {
                    producer.Produce(topicVariable, new Message<string, object>());
                }
            }
            """;
        var unboundScan = new ScanResult();
        ReadCSharp(path, unbound, unboundScan);
        unboundScan.Finish();
        Assert.Empty(unboundScan.Producers);
        Assert.Equal(new[] { "UnboundTopicProduce.cs" }, unboundScan.DynamicGapFiles);
        Assert.NotEqual(KnownDynamicGapFiles, unboundScan.DynamicGapFiles);
    }

    private static bool CatalogHas(CodeHit hit, bool producers)
    {
        var family = KafkaTopicCatalog.FamilyOf(hit.Topic);
        var sites = producers ? family.Producers : family.Consumers;
        return sites.Any(site => string.Equals(site.Path, hit.Path, StringComparison.Ordinal));
    }

    private static List<string> Compare(ScanResult scan, string? dropTopic, string? dropPath)
    {
        var problems = new List<string>(scan.Problems);
        foreach (var hit in scan.Producers)
            Collect(problems, hit, producers: true, dropTopic, dropPath);
        foreach (var hit in scan.Consumers)
            Collect(problems, hit, producers: false, dropTopic, dropPath);
        return problems;
    }

    private static void Collect(List<string> problems, CodeHit hit, bool producers, string? dropTopic, string? dropPath)
    {
        var family = KafkaTopicCatalog.FamilyOf(hit.Topic);
        var sites = (producers ? family.Producers : family.Consumers)
            .Where(site => string.Equals(site.Path, hit.Path, StringComparison.Ordinal))
            .ToList();
        if (producers
            && dropPath != null
            && string.Equals(hit.Topic, dropTopic, StringComparison.Ordinal)
            && string.Equals(hit.Path, dropPath, StringComparison.Ordinal))
        {
            sites.Clear();
        }

        var role = producers ? "producer" : "consumer";
        if (sites.Count == 0)
        {
            problems.Add(role + " missing from " + hit.Topic + ": " + hit.Path);
            return;
        }

        if (hit.ControlPlane && !sites.Any(site => site.ControlPlane))
            problems.Add("control-plane " + role + " is not marked ControlPlane: " + hit.Topic + " " + hit.Path);
    }

    private static ScanResult Scan(string root)
    {
        var scan = new ScanResult();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file))
                continue;
            ReadCSharp(Relative(root, file), File.ReadAllText(file), scan);
        }

        var javaTopics = ReadJavaTopics(Path.Combine(root, "Java", "shared", "src", "main", "java", "com", "lantanagroup", "link", "shared", "kafka", "Topics.java"));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Java"), "*.java", SearchOption.AllDirectories))
        {
            if (IsJavaExcluded(file))
                continue;
            ReadJava(Relative(root, file), File.ReadAllText(file), javaTopics, scan);
        }

        scan.Finish();
        return scan;
    }

    private static void ReadCSharp(string path, string text, ScanResult scan)
    {
        var bindings = ReadBindings(text);
        AddProduces(text, path, bindings, scan);
        AddSubscribes(text, path, bindings, scan);
        AddTopicLists(text, path, scan);
        foreach (Match match in BaseListenerPattern.Matches(text))
            AddHit(scan.Consumers, match.Groups[1].Value, path);
    }

    private static void AddProduces(string text, string path, Dictionary<string, Binding> bindings, ScanResult scan)
    {
        foreach (var name in new[] { "ProduceAsync", "Produce" })
        {
            var index = 0;
            while ((index = FindCall(text, name, index)) >= 0)
            {
                var open = text.IndexOf('(', index);
                var inner = ReadCall(text, open, path);
                ClassifyProduce(inner, path, bindings, scan);
                index = open + inner.Length + 2;
            }
        }
    }

    private static void ClassifyProduce(string inner, string path, Dictionary<string, Binding> bindings, ScanResult scan)
    {
        var args = SplitArgs(inner);
        if (args.Count == 0)
            return;
        var first = args[0].Trim();
        if (first.StartsWith("topic:", StringComparison.Ordinal))
            first = first["topic:".Length..].Trim();
        if (TryResolveConstant(first, bindings, out var topic))
        {
            AddHit(scan.Producers, topic, path);
            return;
        }

        if (!IsKafkaClientProduce(first, args))
            return;

        scan.NoteDynamicGap(path);
    }

    private static bool IsKafkaClientProduce(string first, List<string> args)
    {
        if (first.StartsWith("KafkaTopicNames.Redrive", StringComparison.Ordinal)
            || first.StartsWith("KafkaTopicNames.Retry", StringComparison.Ordinal)
            || first.StartsWith("KafkaTopicNames.Error", StringComparison.Ordinal))
        {
            return true;
        }

        if (args.Count < 2)
            return false;
        var second = args[1].TrimStart();
        return second.StartsWith("new Message<", StringComparison.Ordinal);
    }

    private static void AddSubscribes(string text, string path, Dictionary<string, Binding> bindings, ScanResult scan)
    {
        var index = 0;
        while ((index = FindCall(text, "Subscribe", index)) >= 0)
        {
            var open = text.IndexOf('(', index);
            var inner = ReadCall(text, open, path);
            AddSubscribe(inner, path, bindings, scan);
            index = open + inner.Length + 2;
        }
    }

    private static void AddSubscribe(string inner, string path, Dictionary<string, Binding> bindings, ScanResult scan)
    {
        var argument = inner.Trim();
        if (argument.StartsWith("KafkaTopicNames.Subscription", StringComparison.Ordinal))
        {
            var open = argument.IndexOf('(');
            var subscription = ReadCall(argument, open, path);
            var args = SplitArgs(subscription);
            if (args.Count == 0)
            {
                scan.Problems.Add("empty subscription in " + path);
                return;
            }

            argument = args[0].Trim();
        }

        if (TryResolveConstant(argument, bindings, out var topic))
            AddHit(scan.Consumers, topic, path);
    }

    private static void AddTopicLists(string text, string path, ScanResult scan)
    {
        var index = 0;
        while ((index = text.IndexOf("new List<", index, StringComparison.Ordinal)) >= 0)
        {
            if (IsLineComment(text, index))
            {
                index += "new List<".Length;
                continue;
            }

            var after = SkipAngles(text, text.IndexOf('<', index), path);
            after = SkipSpace(text, after);
            if (after < text.Length && text[after] == '(')
            {
                var inner = ReadCall(text, after, path);
                after = SkipSpace(text, after + inner.Length + 2);
            }

            if (after >= text.Length || text[after] != '{')
            {
                index = Math.Max(after, index + "new List<".Length);
                continue;
            }

            var body = ReadBrace(text, after, path);
            AddTopicRefs(body, path, scan.Consumers);
            index = after + body.Length + 2;
        }
    }

    private static void AddTopicRefs(string body, string path, List<CodeHit> hits)
    {
        foreach (Match match in TopicRefPattern.Matches(body))
            AddHit(hits, match.Groups["member"].Value, path);
    }

    private static bool TryResolveConstant(string expression, Dictionary<string, Binding> bindings, out string topic)
    {
        topic = "";
        var named = NameofTopicPattern.Match(expression);
        if (named.Success && expression == named.Value)
        {
            topic = named.Groups["member"].Value;
            return true;
        }

        var asString = ToStringTopicPattern.Match(expression);
        if (asString.Success && expression == asString.Value)
        {
            topic = asString.Groups["member"].Value;
            return true;
        }

        if (IdentifierPattern.IsMatch(expression)
            && bindings.TryGetValue(expression, out var binding)
            && binding.Kind == BindingKind.Topic)
        {
            topic = binding.Topic;
            return true;
        }

        return false;
    }

    private static Dictionary<string, Binding> ReadBindings(string text)
    {
        var map = new Dictionary<string, Binding>(StringComparer.Ordinal);
        foreach (Match match in AssignmentPattern.Matches(text))
        {
            var name = match.Groups["name"].Value;
            Binding binding;
            if (match.Groups["value"].Value.StartsWith("KafkaTopicNames.Redrive", StringComparison.Ordinal))
                binding = new Binding(BindingKind.Redrive, "");
            else
                binding = new Binding(BindingKind.Topic, match.Groups["member"].Value);
            if (map.TryGetValue(name, out var existing) && existing != binding)
                map[name] = new Binding(BindingKind.Conflict, "");
            else
                map[name] = binding;
        }

        return map;
    }

    private static void ReadJava(string path, string text, Dictionary<string, string> topics, ScanResult scan)
    {
        var index = 0;
        while ((index = text.IndexOf("new ProducerRecord", index, StringComparison.Ordinal)) >= 0)
        {
            if (IsLineComment(text, index))
            {
                index += "new ProducerRecord".Length;
                continue;
            }

            var open = text.IndexOf('(', index);
            var inner = ReadCall(text, open, path);
            var args = SplitArgs(inner);
            if (args.Count > 0)
                AddJavaProduce(args[0], path, topics, scan);
            index = open + inner.Length + 2;
        }

        index = 0;
        while ((index = FindCall(text, "send", index)) >= 0)
        {
            var open = text.IndexOf('(', index);
            var inner = ReadCall(text, open, path);
            var args = SplitArgs(inner);
            if (args.Count > 0 && !args[0].Contains("new ProducerRecord", StringComparison.Ordinal))
                AddJavaProduce(args[0], path, topics, scan);
            index = open + inner.Length + 2;
        }

        index = 0;
        while ((index = text.IndexOf("@KafkaListener(", index, StringComparison.Ordinal)) >= 0)
        {
            if (IsLineComment(text, index))
            {
                index += "@KafkaListener(".Length;
                continue;
            }

            var open = text.IndexOf('(', index);
            var body = ReadCall(text, open, path);
            AddJavaListener(body, path, topics, scan);
            index = open + body.Length + 2;
        }

        foreach (var name in new[] { "includeTopics", "subscribe" })
        {
            index = 0;
            while ((index = FindCall(text, name, index)) >= 0)
            {
                var open = text.IndexOf('(', index);
                var body = ReadCall(text, open, path);
                AddJavaTopicRefs(body, path, topics, scan.Consumers, scan, required: true);
                index = open + body.Length + 2;
            }
        }
    }

    private static void AddJavaProduce(string expression, string path, Dictionary<string, string> topics, ScanResult scan)
    {
        var value = expression.Trim();
        var match = JavaTopicPattern.Match(value);
        if (match.Success && value == match.Value)
        {
            AddJavaTopic(match.Groups["name"].Value, path, topics, scan.Producers, scan);
            return;
        }

        if (IdentifierPattern.IsMatch(value) || value.StartsWith("KafkaTopicNames.", StringComparison.Ordinal))
            scan.NoteDynamicGap(path);
        else
            scan.Problems.Add("unresolved java produce in " + path + ": " + OneLine(value));
    }

    private static void AddJavaListener(string body, string path, Dictionary<string, string> topics, ScanResult scan)
    {
        var assignment = Regex.Match(body, @"topics\s*=\s*(?<value>\{[^}]*\}|Topics\.\w+)", RegexOptions.CultureInvariant);
        if (!assignment.Success)
        {
            scan.Problems.Add("kafka listener topic was not resolved in " + path);
            return;
        }

        AddJavaTopicRefs(assignment.Groups["value"].Value, path, topics, scan.Consumers, scan, required: true);
    }

    private static void AddJavaTopicRefs(string body, string path, Dictionary<string, string> topics, List<CodeHit> hits, ScanResult scan, bool required)
    {
        var found = false;
        foreach (Match match in JavaTopicPattern.Matches(body))
        {
            AddJavaTopic(match.Groups["name"].Value, path, topics, hits, scan);
            found = true;
        }

        if (required && !found)
            scan.Problems.Add("topic array was not resolved in " + path);
    }

    private static void AddJavaTopic(string constant, string path, Dictionary<string, string> topics, List<CodeHit> hits, ScanResult scan)
    {
        if (!topics.TryGetValue(constant, out var name))
        {
            scan.Problems.Add("unknown topic Topics." + constant + " in " + path);
            return;
        }

        AddHit(hits, KafkaTopicCatalog.MainName(name), path);
    }

    private static Dictionary<string, string> ReadJavaTopics(string path)
    {
        var topics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), "public static final String (\\w+) = \"([^\"]+)\";", RegexOptions.CultureInvariant))
            topics[match.Groups[1].Value] = match.Groups[2].Value;
        if (topics.Count == 0)
            throw new InvalidOperationException("Java topic constants were not found.");
        return topics;
    }

    private static void AddHit(List<CodeHit> hits, string topic, string path)
    {
        if (topic.Length == 0)
            return;
        if (hits.Any(hit => string.Equals(hit.Topic, topic, StringComparison.Ordinal) && string.Equals(hit.Path, path, StringComparison.Ordinal)))
            return;
        hits.Add(new CodeHit(topic, path, IsControlPlane(path)));
    }

    private static bool IsControlPlane(string path) =>
        path.Contains("DotNet/Admin.BFF/Application/Commands/Integration/", StringComparison.Ordinal);

    private static int FindCall(string text, string name, int start)
    {
        var needle = "." + name;
        var index = start;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            var cursor = index + needle.Length;
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                cursor++;
            if (cursor < text.Length && text[cursor] == '(' && !IsLineComment(text, index))
                return index;
            index += needle.Length;
        }

        return -1;
    }

    private static bool IsLineComment(string text, int index)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var line = text[lineStart..index];
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 && !line[..comment].Contains('"', StringComparison.Ordinal);
    }

    private static string ReadCall(string text, int openParen, string path)
    {
        var depth = 0;
        for (var i = openParen; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return text[(openParen + 1)..i];
            }
        }

        throw new InvalidOperationException("Unbalanced call in " + path);
    }

    private static string ReadBrace(string text, int openBrace, string path)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[(openBrace + 1)..i];
            }
        }

        throw new InvalidOperationException("Unbalanced list in " + path);
    }

    private static int SkipAngles(string text, int openAngle, string path)
    {
        var depth = 0;
        for (var i = openAngle; i < text.Length; i++)
        {
            if (text[i] == '<')
                depth++;
            else if (text[i] == '>')
            {
                depth--;
                if (depth == 0)
                    return i + 1;
            }
        }

        throw new InvalidOperationException("Unbalanced generic in " + path);
    }

    private static int SkipSpace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index;
    }

    private static List<string> SplitArgs(string inner)
    {
        var args = new List<string>();
        var depth = 0;
        var angle = 0;
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '(' || c == '{')
                depth++;
            else if (c == ')' || c == '}')
                depth--;
            else if (c == '<')
                angle++;
            else if (c == '>')
                angle--;
            else if (c == ',' && depth == 0 && angle == 0)
            {
                args.Add(inner[start..i].Trim());
                start = i + 1;
            }
        }

        var last = inner[start..].Trim();
        if (last.Length > 0 || args.Count > 0)
            args.Add(last);
        return args;
    }

    private static bool IsExcluded(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (part is "bin" or "obj" or "ServiceTests" or "KafkaKeyProof.Tests")
                return true;
            if (part.EndsWith(".Tests", StringComparison.Ordinal) || part.Equals("tests", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return Path.GetFileName(file).EndsWith("Tests.cs", StringComparison.Ordinal);
    }

    private static bool IsJavaExcluded(string file)
    {
        var normalized = file.Replace('\\', '/');
        return normalized.Contains("/src/test/", StringComparison.Ordinal)
            || normalized.EndsWith("/RetryTopicBackoffDelayTest.java", StringComparison.Ordinal)
            || !normalized.Contains("/src/main/", StringComparison.Ordinal);
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string OneLine(string value)
    {
        var line = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 160 ? line : line[..160];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from the test output directory.");
    }

    private enum BindingKind
    {
        Topic,
        Redrive,
        Conflict
    }

    private readonly record struct Binding(BindingKind Kind, string Topic);

    private readonly record struct CodeHit(string Topic, string Path, bool ControlPlane);

    private sealed class ScanResult
    {
        public List<CodeHit> Producers { get; } = new();
        public List<CodeHit> Consumers { get; } = new();
        public List<string> Problems { get; } = new();
        public string[] DynamicGapFiles { get; private set; } = [];
        private readonly HashSet<string> _gaps = new(StringComparer.Ordinal);

        public void NoteDynamicGap(string path) => _gaps.Add(Path.GetFileName(path));

        public void Finish() => DynamicGapFiles = _gaps.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static readonly Regex AssignmentPattern = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>nameof\(\s*KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\)|KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\.ToString\(\s*\)|KafkaTopicNames\.Redrive\s*\([^;]*\))\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NameofTopicPattern = new(
        @"^nameof\(\s*KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ToStringTopicPattern = new(
        @"^KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\.ToString\(\s*\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TopicRefPattern = new(
        @"nameof\(\s*KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\)|KafkaTopic\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\.ToString\(\s*\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex JavaTopicPattern = new(
        @"Topics\.(?<name>[A-Z0-9_]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BaseListenerPattern = new(
        @"class\s+\w+\s*:\s*BaseListener<\s*(?<topic>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
