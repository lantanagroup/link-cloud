namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class ProofKitTests
{
    [Fact]
    public void ComposeFile_PublishesNoHostPorts_AndNamesTheProofServices()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "Scripts", "kafka-ops-proof", "compose.yml"));
        Assert.DoesNotContain("container_name", text, StringComparison.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            Assert.False(line.TrimStart().StartsWith("ports:", StringComparison.Ordinal), line);
        }

        Assert.Contains("broker-0:", text, StringComparison.Ordinal);
        Assert.Contains("broker-1:", text, StringComparison.Ordinal);
        Assert.Contains("broker-2:", text, StringComparison.Ordinal);
        Assert.Contains("broker-3:", text, StringComparison.Ordinal);
        Assert.Contains("extra-broker", text, StringComparison.Ordinal);
        Assert.Contains("consumer:", text, StringComparison.Ordinal);
        Assert.Contains("ops-proof-log", text, StringComparison.Ordinal);
        Assert.Contains("ops-proof-consumers", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_RequiresTheAllowLatch_AndDoesNotDeleteVolumes()
    {
        var root = RepoRoot();
        foreach (var name in new[] { "run-proof.ps1", "run-proof.sh" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Scripts", "kafka-ops-proof", name));
            Assert.Contains("KAFKA_PROOF_ALLOW", text, StringComparison.Ordinal);
            Assert.DoesNotContain("down -v", text, StringComparison.Ordinal);
            Assert.DoesNotContain("down --volumes", text, StringComparison.Ordinal);
            Assert.Contains("ops-proof-consumers", text, StringComparison.Ordinal);
            Assert.Contains("broker-3", text, StringComparison.Ordinal);
        }
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

        throw new InvalidOperationException("The repository root was not found from the test output directory.");
    }
}
