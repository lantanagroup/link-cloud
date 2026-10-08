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
        Assert.Contains("HOST://localhost:19094", text, StringComparison.Ordinal);
        Assert.Contains("HOST://localhost:19095", text, StringComparison.Ordinal);
        Assert.Contains("HOST://localhost:19096", text, StringComparison.Ordinal);
        Assert.Contains("HOST://localhost:19097", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishFile_DoesNotChangeBrokerZero()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "Scripts", "kafka-ops-proof", "compose.publish.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.DoesNotContain("\n  broker-0:", text, StringComparison.Ordinal);
        Assert.Contains("host-proxy-0", text, StringComparison.Ordinal);
        Assert.Contains("alpine/socat", text, StringComparison.Ordinal);
        Assert.Contains("extra-broker", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_RequiresTheAllowLatch_AndRemovesVolumes()
    {
        var root = RepoRoot();
        foreach (var name in new[] { "run-proof.ps1", "run-proof.sh" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Scripts", "kafka-ops-proof", name));
            Assert.Contains("KAFKA_PROOF_ALLOW", text, StringComparison.Ordinal);
            Assert.Contains("down -v", text, StringComparison.Ordinal);
            Assert.Contains("--remove-orphans", text, StringComparison.Ordinal);
            Assert.Contains("docker volume ls -q --filter \"label=com.docker.compose.project=", text, StringComparison.Ordinal);
            Assert.Contains("docker volume rm", text, StringComparison.Ordinal);
            Assert.Contains("extra-broker", text, StringComparison.Ordinal);
            Assert.Contains("ops-proof-consumers", text, StringComparison.Ordinal);
            Assert.Contains("broker-3", text, StringComparison.Ordinal);
            Assert.Contains("KafkaOps.Proof", text, StringComparison.Ordinal);
            Assert.Contains("KafkaOpsConsoleFlowTests", text, StringComparison.Ordinal);
            Assert.Contains("exit 2", text, StringComparison.Ordinal);
        }

        var flow = File.ReadAllText(Path.Combine(root, "DotNet", "KafkaOps.Proof", "KafkaOpsConsoleFlowTests.cs"));
        Assert.Contains("Skip = \"KAFKA_BOOTSTRAP is not set.\"", flow, StringComparison.Ordinal);
        Assert.Contains("AddSeconds(30)", flow, StringComparison.Ordinal);

        var windows = File.ReadAllText(Path.Combine(root, "Scripts", "kafka-ops-proof", "run-proof.ps1"));
        Assert.DoesNotContain("Write-Error", windows, StringComparison.Ordinal);
        Assert.Contains("dotnet is not on PATH", windows, StringComparison.Ordinal);
        Assert.Contains("KAFKA_BOOTSTRAP is not set", windows, StringComparison.Ordinal);
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
