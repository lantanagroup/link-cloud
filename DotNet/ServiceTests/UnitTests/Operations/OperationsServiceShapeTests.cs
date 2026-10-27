using System.Text.Json;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Filters;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Operations;

/// <summary>
/// The Kafka console backend lives in the Operations service. The Admin BFF only authenticates the
/// caller and proxies /api/ops/kafka there with a Link bearer token carrying the user's claims.
/// </summary>
[Trait("Category", "UnitTests")]
public sealed class OperationsServiceShapeTests
{
    [Fact]
    public void Bff_ProxiesTheKafkaConsoleRoutes_ToTheOperationsCluster_AsAnAuthenticatedUser()
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(DotNet(), "Admin.BFF", "appsettings.json")));
        var proxy = settings.RootElement.GetProperty("ReverseProxy");
        var route = proxy.GetProperty("Routes").EnumerateObject()
            .Single(item => item.Value.GetProperty("ClusterId").GetString() == "OperationsService").Value;

        Assert.Equal("api/ops/kafka/{**catch-all}", route.GetProperty("Match").GetProperty("Path").GetString());
        // A route with a policy is what makes the BFF mint the Link bearer token with the user's claims.
        Assert.Equal("AuthenticatedUser", route.GetProperty("AuthorizationPolicy").GetString());
        Assert.True(proxy.GetProperty("Clusters").TryGetProperty("OperationsService", out _));
    }

    [Fact]
    public async Task Bff_ResolvesTheOperationsClusterFromTheServiceRegistry()
    {
        var registry = new ServiceRegistry { OperationsServiceUrl = "http://operations:8079", TenantService = new TenantServiceRegistration() };
        var filter = new YarpConfigFilter(NullLogger<YarpConfigFilter>.Instance, Options.Create(registry));
        var cluster = new ClusterConfig
        {
            ClusterId = "OperationsService",
            Destinations = new Dictionary<string, DestinationConfig> { ["destination1"] = new() { Address = "" } }
        };

        var result = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);

        Assert.Equal("http://operations:8079", result.Destinations!["destination1"].Address);
    }

    [Fact]
    public void Bff_HoldsNoKafkaAdminLogic()
    {
        var bff = Path.Combine(DotNet(), "Admin.BFF");
        Assert.False(Directory.Exists(Path.Combine(bff, "Application", "KafkaOps")));
        Assert.False(File.Exists(Path.Combine(bff, "Presentation", "Endpoints", "KafkaOpsEndpoints.cs")));
        var program = File.ReadAllText(Path.Combine(bff, "Program.cs"));
        foreach (var gone in new[] { "KafkaOpsService", "KafkaOpsWorker", "KafkaOpsEndpoints", "MigrationRuntime", "KafkaAdminWire", "IKafkaOpsLease" })
            Assert.DoesNotContain(gone, program, StringComparison.Ordinal);
    }

    [Fact]
    public void Operations_OwnsTheConsole_WithDurableStoresAndTheBrokerLifecycleFlagOff()
    {
        var operations = Path.Combine(DotNet(), "Operations");
        var program = File.ReadAllText(Path.Combine(operations, "Program.cs"));
        Assert.Contains("AddTransient<IApi, KafkaOpsEndpoints>", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<KafkaOpsWorker>", program, StringComparison.Ordinal);
        Assert.Contains("SqlChangeRequestStore", program, StringComparison.Ordinal);
        Assert.Contains("SqlExecutorLease", program, StringComparison.Ordinal);
        Assert.Contains("AddLinkBearerServiceAuthentication", program, StringComparison.Ordinal);
        Assert.Contains("AutoMigrateEF<OperationsDbContext>", program, StringComparison.Ordinal);

        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Docker.json" })
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(operations, file)));
            Assert.False(settings.RootElement.GetProperty("KafkaOps").GetProperty("BrokerLifecycleEnabled").GetBoolean(), file);
        }

        using var production = JsonDocument.Parse(File.ReadAllText(Path.Combine(operations, "appsettings.json")));
        Assert.True(production.RootElement.GetProperty("KafkaOps").GetProperty("TwoPersonApproval").GetBoolean());
        Assert.Equal("SqlServer", production.RootElement.GetProperty("DatabaseProvider").GetString());

        foreach (var local in new[] { "appsettings.Development.json", "appsettings.Docker.json" })
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(operations, local)));
            var kafkaOps = settings.RootElement.GetProperty("KafkaOps");
            Assert.False(kafkaOps.GetProperty("TwoPersonApproval").GetBoolean(), local);
            Assert.False(kafkaOps.GetProperty("RequireDifferentApprover").GetBoolean(), local);
        }
    }

    [Fact]
    public void Compose_RunsOperations_AndTheBffWaitsForIt()
    {
        var compose = File.ReadAllText(Path.Combine(DotNet(), "..", "docker-compose.yml")).Replace("\r\n", "\n");
        Assert.Contains("  operations:\n    image: link-operations\n    container_name: link-operations\n", compose, StringComparison.Ordinal);
        Assert.Contains("      - \"8091:8079\"", compose, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8079/health", compose, StringComparison.Ordinal);
        Assert.Contains("ServiceRegistry__OperationsServiceUrl: http://operations:8079", compose, StringComparison.Ordinal);
        Assert.Contains("Initial Catalog=link-operations;", compose, StringComparison.Ordinal);
        Assert.Contains("[link-operations]", File.ReadAllText(Path.Combine(DotNet(), "..", ".docker", "create-dbs.sql")), StringComparison.Ordinal);
    }

    private static string DotNet()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "DotNet");
            if (Directory.Exists(Path.Combine(candidate, "Operations")) && Directory.Exists(Path.Combine(candidate, "Admin.BFF")))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The DotNet folder was not found from the test output directory.");
    }
}
