namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class ProducerPartitionerGuardTests
{
    [Fact]
    public void EveryProducerBuilderGoesThroughMurmur2Defaults()
    {
        var root = FindRepoRoot();
        var failures = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (text.Contains("new ProducerBuilder", StringComparison.Ordinal)
                && !text.Contains("KafkaClientDefaults.ApplyProducer", StringComparison.Ordinal)
                && !text.Contains("CreateProducerConfig(", StringComparison.Ordinal))
            {
                failures.Add(file);
            }

            if (text.Contains("Partitioner =", StringComparison.Ordinal)
                && !file.EndsWith("KafkaClientDefaults.cs", StringComparison.Ordinal)
                && !file.EndsWith("ProducerPartitionerGuardTests.cs", StringComparison.Ordinal)
                && !file.EndsWith("KafkaTopicAndAssignmentTests.cs", StringComparison.Ordinal))
            {
                failures.Add(file + " sets Partitioner outside KafkaClientDefaults");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static bool IsExcluded(string file)
    {
        var normalized = file.Replace('/', '\\');
        return normalized.Contains("\\obj\\", StringComparison.Ordinal)
            || normalized.Contains("\\bin\\", StringComparison.Ordinal)
            || normalized.Contains("\\ServiceTests\\", StringComparison.Ordinal)
            || normalized.Contains("\\KafkaKeyProof.Tests\\", StringComparison.Ordinal)
            // The console proof seeds one record so throwaway groups have a member. The key is a fixed probe value, not a facility or patient key.
            || normalized.EndsWith("\\KafkaOps.Proof\\KafkaOpsConsoleFlowTests.cs", StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from the test output directory.");
    }
}
