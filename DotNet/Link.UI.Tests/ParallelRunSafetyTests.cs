using System.Text.Json.Nodes;
using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Configuration;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Automation.Helpers;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ParallelRunSafetyTests
{
    private const string SharedBundle = """
        {
          "resourceType": "Bundle",
          "type": "transaction",
          "entry": [
            {
              "fullUrl": "Patient/pat-1",
              "resource": {
                "resourceType": "Patient",
                "id": "pat-1"
              },
              "request": { "method": "PUT", "url": "Patient/pat-1" }
            },
            {
              "fullUrl": "Encounter/enc-1",
              "resource": {
                "resourceType": "Encounter",
                "id": "enc-1",
                "subject": { "reference": "Patient/pat-1" },
                "location": [
                  { "location": { "reference": "Location/loc-1/_history/1" } }
                ]
              },
              "request": { "method": "PUT", "url": "Encounter/enc-1" }
            },
            {
              "fullUrl": "Location/loc-1",
              "resource": {
                "resourceType": "Location",
                "id": "loc-1",
                "partOf": { "reference": "Location/loc-10" }
              },
              "request": { "method": "PUT", "url": "Location/loc-1" }
            },
            {
              "fullUrl": "Location/loc-10",
              "resource": { "resourceType": "Location", "id": "loc-10" },
              "request": { "method": "PUT", "url": "Location/loc-10" }
            }
          ]
        }
        """;

    [Fact]
    public void Two_runs_on_the_same_ehr_get_different_facilities()
    {
        var firstRun = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var secondRun = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var noneTaken = new HashSet<string>(StringComparer.Ordinal);

        var guidFacility = RunOrganizationIdAllocator.Choose(false, firstRun, noneTaken);
        var otherGuidFacility = RunOrganizationIdAllocator.Choose(false, secondRun, noneTaken);
        guidFacility.Should().Be(firstRun.ToString());
        otherGuidFacility.Should().NotBe(guidFacility);

        var taken = RunOrganizationIdAllocator.CollectInUse(
        [
            (guidFacility, guidFacility, true)
        ]);
        var numericFacility = RunOrganizationIdAllocator.Choose(
            numericOnlyFacilityId: true,
            firstRun,
            taken,
            (_, _) => 10_000);
        var nextTaken = RunOrganizationIdAllocator.CollectInUse(
        [
            (guidFacility, guidFacility, true),
            (numericFacility, numericFacility, true)
        ]);
        var reuse = () => RunOrganizationIdAllocator.Choose(
            numericOnlyFacilityId: true,
            secondRun,
            nextTaken,
            (_, _) => 10_000);
        reuse.Should().Throw<InvalidOperationException>();

        var freeDraws = new Queue<int>([10_000, 10_001]);
        var firstNumeric = RunOrganizationIdAllocator.Choose(
            true, firstRun, new HashSet<string>(StringComparer.Ordinal), (_, _) => freeDraws.Dequeue());
        var held = RunOrganizationIdAllocator.CollectInUse([(firstNumeric, firstNumeric, true)]);
        var secondFree = RunOrganizationIdAllocator.Choose(
            true, secondRun, held, (_, _) => freeDraws.Dequeue());
        secondFree.Should().NotBe(firstNumeric);
        ParallelRunSafety.FacilityIsHeldByAnotherRun(firstNumeric, [secondFree]).Should().BeFalse();
        ParallelRunSafety.FacilityIsHeldByAnotherRun(firstNumeric, [firstNumeric]).Should().BeTrue();
    }

    [Fact]
    public void One_runs_success_cleanup_does_not_expunge_the_other_runs_imported_resources()
    {
        var first = IsolateCopy(SharedBundle, "aaaa1111");
        var second = IsolateCopy(SharedBundle, "bbbb2222");

        first.Paths.Should().NotIntersectWith(second.Paths);
        first.PatientId.Should().Be("aaaa1111-pat-1");
        second.PatientId.Should().Be("bbbb2222-pat-1");
        var root = JsonNode.Parse(first.Json)!.AsObject();
        var entries = root["entry"]!.AsArray();
        Reference(entries, "Encounter", "subject").Should().Be("Patient/aaaa1111-pat-1");
        Reference(entries, "Encounter", "location", 0, "location").Should().Be("Location/aaaa1111-loc-1/_history/1");
        Reference(entries, "Location", "partOf").Should().Be("Location/aaaa1111-loc-10");
        RequestUrl(entries, "Patient").Should().Be("Patient/aaaa1111-pat-1");
        first.Json.Should().NotContain("Patient/pat-1");
        first.Json.Should().NotContain("Location/loc-1/");

        var safeForFirst = ParallelRunSafety.PathsSafeToExpunge(first.Paths, second.Paths);
        safeForFirst.Should().BeEquivalentTo(first.Paths);
        safeForFirst.Should().NotIntersectWith(second.Paths);

        var overlap = first.Paths.Append("Patient/shared").ToArray();
        var stillShared = ParallelRunSafety.PathsSafeToExpunge(overlap, ["Patient/shared"]);
        stillShared.Should().NotContain("Patient/shared");
        stillShared.Should().Contain("Patient/aaaa1111-pat-1");
    }

    [Fact]
    public void An_existing_patient_id_is_not_rewritten()
    {
        var imported = new ImportedPatientInput
        {
            Source = ImportedPatientSource.ExistingId,
            PatientId = "already-there"
        };

        var paths = ImportedResourceRunScope.Isolate(imported, "aaaa1111");

        imported.PatientId.Should().Be("already-there");
        paths.Should().Equal("Patient/already-there");
    }

    [Fact]
    public void A_long_logical_id_stays_within_the_fhir_limit()
    {
        var original = new string('a', 64);
        var scoped = ImportedResourceRunScope.ScopeLogicalId("aaaa1111", original);

        scoped.Length.Should().BeLessThanOrEqualTo(ImportedResourceRunScope.MaxLogicalIdLength);
        scoped.Should().StartWith("aaaa1111-");
        scoped.Should().NotBe(original);
    }

    [Fact]
    public void Cleanup_keeps_a_tracked_create_another_run_on_the_same_ehr_still_claims()
    {
        var registry = new SharedEhrWorkRegistry();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        registry.Begin(first, "http://ehr.example/fhir", "10001");
        registry.Begin(second, "http://ehr.example/fhir", "10002");
        registry.Claim(first, ["Patient/run-a", "Encounter/run-a"]);
        registry.Claim(second, ["Patient/run-a", "Patient/run-b"]);

        var safe = ParallelRunSafety.PathsSafeToExpunge(
            ["Patient/run-a", "Encounter/run-a"],
            registry.PathsHeldByOthers(first));

        safe.Should().Equal("Encounter/run-a");
        registry.FacilitiesHeldByOthers(first).Should().Equal("10002");
        ParallelRunSafety.FacilityIsHeldByAnotherRun("10001", registry.FacilitiesHeldByOthers(first)).Should().BeFalse();
        ParallelRunSafety.FacilityIsHeldByAnotherRun("10002", registry.FacilitiesHeldByOthers(first)).Should().BeTrue();

        registry.Release(second);
        ParallelRunSafety.PathsSafeToExpunge(
                ["Patient/run-a", "Encounter/run-a"],
                registry.PathsHeldByOthers(first))
            .Should().BeEquivalentTo(["Patient/run-a", "Encounter/run-a"]);
    }

    [Fact]
    public void A_different_ehr_does_not_block_expunge_of_the_same_path()
    {
        var registry = new SharedEhrWorkRegistry();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        registry.Begin(first, "http://ehr-a.example/fhir", "10001");
        registry.Begin(second, "http://ehr-b.example/fhir", "10002");
        registry.Claim(first, ["Patient/pat-1"]);
        registry.Claim(second, ["Patient/pat-1"]);

        registry.PathsHeldByOthers(first).Should().BeEmpty();
        registry.FacilitiesHeldByOthers(first).Should().BeEmpty();
        ParallelRunSafety.FacilityIsHeldByAnotherRun("10001", registry.FacilitiesHeldByOthers(first))
            .Should().BeFalse();
    }

    [Fact]
    public void Two_named_ehr_systems_keep_separate_runs_and_the_same_endpoint_stays_protected()
    {
        var host = new AutomationConfig
        {
            FhirServerBase = "http://host.example/fhir",
            FhirServerOAuth = new OAuthConfig { ClientId = "host-client" },
            FhirServerBasicAuth = new BasicAuthConfig()
        };
        var local = System("local", "https://ehr-a.example/fhir", isDefault: true);
        var sandbox = System("sandbox", "https://ehr-b.example/fhir", isDefault: false);
        var sameServer = System("alias", "https://ehr-a.example/fhir", isDefault: false);
        var localKey = EhrEndpointIdentity.From(EhrSystemCatalog.WithEndpoints(host, local));
        var sandboxKey = EhrEndpointIdentity.From(EhrSystemCatalog.WithEndpoints(host, sandbox));
        var aliasKey = EhrEndpointIdentity.From(EhrSystemCatalog.WithEndpoints(host, sameServer));

        localKey.Should().NotBe(sandboxKey);
        localKey.Should().Be(aliasKey);
        localKey.Should().NotContain("host-client");

        var registry = new SharedEhrWorkRegistry();
        var onLocal = Guid.NewGuid();
        var onSandbox = Guid.NewGuid();
        registry.Begin(onLocal, localKey, "10001");
        registry.Begin(onSandbox, sandboxKey, "10002");
        registry.Claim(onLocal, ["Patient/pat-1"]);
        registry.Claim(onSandbox, ["Patient/pat-1"]);

        ParallelRunSafety.PathsSafeToExpunge(["Patient/pat-1"], registry.PathsHeldByOthers(onLocal))
            .Should().Equal("Patient/pat-1");
        registry.FacilitiesHeldByOthers(onLocal).Should().BeEmpty();
        ParallelRunSafety.FacilityIsHeldByAnotherRun("10001", registry.FacilitiesHeldByOthers(onLocal))
            .Should().BeFalse();

        var alsoOnLocal = Guid.NewGuid();
        registry.Begin(alsoOnLocal, aliasKey, "10003");
        registry.Claim(alsoOnLocal, ["Patient/pat-1"]);

        registry.PathsHeldByOthers(onLocal).Should().Equal("Patient/pat-1");
        registry.FacilitiesHeldByOthers(onLocal).Should().Equal("10003");
        ParallelRunSafety.FacilityIsHeldByAnotherRun("10003", registry.FacilitiesHeldByOthers(onLocal))
            .Should().BeTrue();
        ParallelRunSafety.PathsSafeToExpunge(["Patient/pat-1"], registry.PathsHeldByOthers(onSandbox))
            .Should().Equal("Patient/pat-1");
    }

    [Fact]
    public void The_loader_drops_only_the_paths_another_run_still_holds()
    {
        var loader = new FhirDataLoader("http://localhost/fhir");
        loader.TrackTransactionResponseForCleanup(
            """
            {
              "resourceType": "Bundle",
              "type": "transaction-response",
              "entry": [
                { "response": { "status": "201 Created", "location": "Patient/run-a/_history/1" } },
                { "response": { "status": "201 Created", "location": "Patient/shared/_history/1" } },
                { "response": { "status": "200 OK", "location": "Patient/pre-existing/_history/4" } }
              ]
            }
            """,
            new SilentOutput());

        var forgotten = loader.ForgetCreatedResources(["Patient/shared"]);

        forgotten.Should().Be(1);
        loader.CreatedResourcePaths.Should().Equal("Patient/run-a");
        loader.CreatedResourcePaths.Should().NotContain("Patient/pre-existing");
    }

    [Fact]
    public void A_second_metrics_run_is_blocked_and_a_normal_run_is_not()
    {
        ParallelRunSafety.BlocksMetricsStart(startingMetricsRun: true, anotherMetricsRunIsOngoing: true)
            .Should().BeTrue();
        ParallelRunSafety.BlocksMetricsStart(startingMetricsRun: true, anotherMetricsRunIsOngoing: false)
            .Should().BeFalse();
        ParallelRunSafety.BlocksMetricsStart(startingMetricsRun: false, anotherMetricsRunIsOngoing: true)
            .Should().BeFalse();

        LinkAutomationStartRules.ExplainStartFailure(
                new InvalidOperationException(ParallelRunSafety.MetricsRunBlockedMessage))
            .Should().Be(ParallelRunSafety.MetricsRunBlockedMessage);
    }

    [Fact]
    public void Ehr_identity_is_the_base_url_and_auth_without_the_secret()
    {
        var first = EhrEndpointIdentity.From(
            "HTTP://EHR.Example/fhir/",
            new OAuthConfig { ShouldAuthenticate = true, TokenEndpoint = "http://auth/token", ClientId = "client", ClientSecret = "secret-one" },
            new BasicAuthConfig());
        var rotatedSecret = EhrEndpointIdentity.From(
            "http://ehr.example/fhir",
            new OAuthConfig { ShouldAuthenticate = true, TokenEndpoint = "http://auth/token", ClientId = "client", ClientSecret = "secret-two" },
            new BasicAuthConfig());
        var otherClient = EhrEndpointIdentity.From(
            "http://ehr.example/fhir",
            new OAuthConfig { ShouldAuthenticate = true, TokenEndpoint = "http://auth/token", ClientId = "other", ClientSecret = "secret-one" },
            new BasicAuthConfig());

        first.Should().Be(rotatedSecret);
        otherClient.Should().NotBe(first);
        first.Should().NotContain("secret");
    }

    [Fact]
    public void The_live_pool_uses_the_scoped_bundle_id_and_not_the_original()
    {
        var isolated = new[]
        {
            new ImportedPatientInput
            {
                Source = ImportedPatientSource.Bundle,
                PatientId = "aaaa1111-pat-1"
            },
            new ImportedPatientInput
            {
                Source = ImportedPatientSource.ExistingId,
                PatientId = "already-there"
            }
        };

        var importedIds = ParallelRunSafety.PatientIdsAfterIsolate(isolated);
        var pool = LivePatientPoolBuilder.Build(
            ["aaaa1111-pat-1", "already-there"],
            profilesAlignedToPatientIds: null,
            importedIds);

        pool.Should().ContainSingle(seed => seed.PatientId == "aaaa1111-pat-1")
            .Which.Origin.Should().Be(LivePatientOrigin.Import);
        pool.Should().ContainSingle(seed => seed.PatientId == "already-there")
            .Which.Origin.Should().Be(LivePatientOrigin.Import);
        pool.Should().NotContain(seed => seed.PatientId == "pat-1");
        pool.Should().HaveCount(2);
    }

    [Fact]
    public void The_run_engine_uses_the_parallel_run_guards()
    {
        var executor = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/RunExecutor.cs"));
        executor.Should().Contain("ImportedResourceRunScope.Isolate");
        executor.Should().Contain("ParallelRunSafety.PathsSafeToExpunge");
        executor.Should().Contain("ForgetCreatedResources");
        executor.Should().Contain("ParallelRunSafety.FacilityIsHeldByAnotherRun");
        executor.Should().Contain("_sharedEhrWork.Release");
        executor.Should().Contain("ParallelRunSafety.PatientIdsAfterIsolate(importedPatients)");
        executor.Should().Contain("EhrEndpointIdentity.From(ConfigFor(state.Options.EhrSystemName))");

        var manager = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/AutomationRunManager.cs"));
        manager.Should().Contain("ParallelRunSafety.BlocksMetricsStart");
        manager.Should().Contain("ParallelRunSafety.MetricsRunBlockedMessage");
        manager.Should().Contain("RunOrganizationIdAllocator.Choose");
    }

    private static string Reference(JsonArray entries, string resourceType, string property, int? arrayIndex = null, string? nested = null)
    {
        var resource = Resource(entries, resourceType);
        JsonNode? node = resource[property];
        if (arrayIndex.HasValue)
            node = node!.AsArray()[arrayIndex.Value];
        if (nested != null)
            node = node![nested];
        if (node is JsonObject obj)
            return obj["reference"]!.GetValue<string>();
        return node!.GetValue<string>();
    }

    private static string RequestUrl(JsonArray entries, string resourceType)
    {
        foreach (var entry in entries)
        {
            if (entry?["resource"]?["resourceType"]?.GetValue<string>() != resourceType)
                continue;
            return entry!["request"]!["url"]!.GetValue<string>();
        }

        throw new InvalidOperationException(resourceType);
    }

    private static JsonObject Resource(JsonArray entries, string resourceType)
    {
        foreach (var entry in entries)
        {
            if (entry?["resource"]?["resourceType"]?.GetValue<string>() == resourceType)
                return entry["resource"]!.AsObject();
        }

        throw new InvalidOperationException(resourceType);
    }

    private static (string PatientId, string Json, IReadOnlyList<string> Paths) IsolateCopy(string bundle, string runTag)
    {
        var imported = new ImportedPatientInput
        {
            Source = ImportedPatientSource.Bundle,
            PatientId = "pat-1",
            BundleJson = bundle
        };
        var paths = ImportedResourceRunScope.Isolate(imported, runTag);
        return (imported.PatientId, imported.BundleJson!, paths);
    }

    private static NamedEhrSystem System(string name, string url, bool isDefault) =>
        new(name, url, url, null, isDefault, new OAuthConfig(), new BasicAuthConfig());

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class SilentOutput : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
