using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Configuration;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Link.UI.Tests;

public class EhrSystemTests
{
    [Fact]
    public void A_named_list_is_the_endpoint_catalog()
    {
        var systems = EhrSystemCatalog.Validate(
        [
            Entry("local", "http://localhost:6157/fhir", "http://fhir-server:8080/fhir", isDefault: true),
            Entry("sandbox", "https://ehr.example/fhir", facilityBase: null, isDefault: false)
        ]);

        systems.Should().HaveCount(2);
        systems[1].FacilityFhirServerBase.Should().Be("https://ehr.example/fhir");
        EhrSystemCatalog.SelectDefault(systems).Name.Should().Be("local");
    }

    [Fact]
    public void One_unmarked_system_is_the_default()
    {
        var systems = EhrSystemCatalog.Validate(
            [Entry("only", "https://ehr.example/fhir", facilityBase: null, isDefault: false)]);

        EhrSystemCatalog.SelectDefault(systems).Name.Should().Be("only");
    }

    [Fact]
    public void An_empty_list_uses_the_single_fhir_base_and_its_auth()
    {
        var legacy = new AutomationConfig
        {
            FhirServerBase = "https://legacy.example/fhir",
            FacilityFhirServerBase = "http://fhir-server:8080/fhir",
            FhirServerBasicAuth = new BasicAuthConfig { ShouldAuthenticate = true, Username = "reader" }
        };
        var systems = EhrSystemCatalog.Parse(new ConfigurationBuilder().Build(), legacy);

        systems.Should().ContainSingle();
        systems[0].Name.Should().Be(EhrSystemCatalog.LegacyName);
        systems[0].FhirServerBase.Should().Be("https://legacy.example/fhir");
        systems[0].FacilityFhirServerBase.Should().Be("http://fhir-server:8080/fhir");
        systems[0].BasicAuth.Username.Should().Be("reader");
        systems[0].BasicAuth.ShouldAuthenticate.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://ehr.example/fhir")]
    [InlineData("https://user:secret@ehr.example/fhir")] // trufflehog:ignore
    public void A_system_requires_an_http_url_without_embedded_credentials(string url)
    {
        var act = () => EhrSystemCatalog.Validate([Entry("local", url, facilityBase: null, isDefault: true)]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Duplicate_names_two_defaults_and_both_auth_modes_are_rejected()
    {
        var duplicate = () => EhrSystemCatalog.Validate(
        [
            Entry("Local", "https://a.example/fhir", null, true),
            Entry("local", "https://b.example/fhir", null, false)
        ]);
        duplicate.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");

        var twoDefaults = () => EhrSystemCatalog.Validate(
        [
            Entry("a", "https://a.example/fhir", null, true),
            Entry("b", "https://b.example/fhir", null, true)
        ]);
        twoDefaults.Should().Throw<InvalidOperationException>().WithMessage("*Only one*");

        var none = () => EhrSystemCatalog.Validate(
        [
            Entry("a", "https://a.example/fhir", null, false),
            Entry("b", "https://b.example/fhir", null, false)
        ]);
        none.Should().Throw<InvalidOperationException>().WithMessage("*default*");

        var bothAuth = () => EhrSystemCatalog.Validate(
        [
            new EhrSystemEntry
            {
                Name = "local",
                FhirServerBase = "https://ehr.example/fhir",
                IsDefault = true,
                OAuth = new OAuthConfig { ShouldAuthenticate = true },
                BasicAuth = new BasicAuthConfig { ShouldAuthenticate = true }
            }
        ]);
        bothAuth.Should().Throw<InvalidOperationException>().WithMessage("*together*");
    }

    [Fact]
    public void Auth_that_is_on_requires_the_fields_the_token_call_uses()
    {
        var missingToken = () => EhrSystemCatalog.Validate([OauthEntry(tokenEndpoint: "  ")]);
        missingToken.Should().Throw<InvalidOperationException>().WithMessage("*OAuth token endpoint*");

        var missingClient = () => EhrSystemCatalog.Validate([OauthEntry(clientId: "")]);
        missingClient.Should().Throw<InvalidOperationException>().WithMessage("*OAuth client id*");

        var missingUser = () => EhrSystemCatalog.Validate([OauthEntry(username: null)]);
        missingUser.Should().Throw<InvalidOperationException>().WithMessage("*OAuth username*");

        var missingPassword = () => EhrSystemCatalog.Validate([OauthEntry(password: null)]);
        missingPassword.Should().Throw<InvalidOperationException>().WithMessage("*OAuth password*");

        var missingBasicUser = () => EhrSystemCatalog.Validate(
        [
            new EhrSystemEntry
            {
                Name = "local",
                FhirServerBase = "https://ehr.example/fhir",
                IsDefault = true,
                BasicAuth = new BasicAuthConfig { ShouldAuthenticate = true, Password = "not-a-real-password" } // trufflehog:ignore
            }
        ]);
        missingBasicUser.Should().Throw<InvalidOperationException>().WithMessage("*basic auth username*");

        var missingBasicPassword = () => EhrSystemCatalog.Validate(
        [
            new EhrSystemEntry
            {
                Name = "local",
                FhirServerBase = "https://ehr.example/fhir",
                IsDefault = true,
                BasicAuth = new BasicAuthConfig { ShouldAuthenticate = true, Username = "reader" }
            }
        ]);
        missingBasicPassword.Should().Throw<InvalidOperationException>().WithMessage("*basic auth password*");

        var configured = EhrSystemCatalog.Validate([OauthEntry()])[0];
        configured.OAuth.ClientSecret.Should().BeNull();
        configured.OAuth.TokenEndpoint.Should().Be("https://auth.example/token");
    }

    [Fact]
    public void Overlay_changes_the_run_endpoint_and_leaves_the_host_config()
    {
        var host = new AutomationConfig
        {
            FhirServerBase = "http://host.example/fhir",
            FacilityFhirServerBase = "http://host-facility.example/fhir",
            LokiBaseUrl = "http://loki:3100",
            FhirServerOAuth = new OAuthConfig { ClientId = "host-client" }
        };
        var catalog = Catalog(
            Entry("local", "http://localhost:6157/fhir", "http://fhir-server:8080/fhir", true),
            Entry("sandbox", "https://sandbox.example/fhir", "https://sandbox-inside.example/fhir", false));

        var overlaid = catalog.Overlay(host, "sandbox");

        overlaid.FhirServerBase.Should().Be("https://sandbox.example/fhir");
        overlaid.FacilityFhirServerBase.Should().Be("https://sandbox-inside.example/fhir");
        overlaid.LokiBaseUrl.Should().Be("http://loki:3100");
        overlaid.FhirServerOAuth.ClientId.Should().BeNull();
        host.FhirServerBase.Should().Be("http://host.example/fhir");
        host.FhirServerOAuth.ClientId.Should().Be("host-client");
    }

    [Fact]
    public async Task A_run_is_tagged_with_the_requested_ehr_and_keeps_a_chosen_template()
    {
        var epic = FacilityTemplateCatalog.EpicId;
        var catalog = Catalog(
            Entry("local", "http://localhost:6157/fhir", null, true, FacilityTemplateCatalog.CernerId.ToString()),
            Entry("sandbox", "https://sandbox.example/fhir", null, false));
        var request = new StartScenarioRequest
        {
            EhrSystem = "sandbox",
            FacilityTemplateId = epic
        };
        var options = FacilityOptions(epic) with { VendorName = "Cerner" };

        var tagged = await EhrSystemRunSelector.ApplyAsync(options, request, catalog, new TemplateStore(), CancellationToken.None);

        tagged.EhrSystemName.Should().Be("sandbox");
        tagged.FacilityTemplateId.Should().Be(epic);
        tagged.VendorName.Should().Be("Cerner");
    }

    [Fact]
    public async Task The_ehr_template_replaces_the_system_default_and_a_name_resolves()
    {
        var cerner = new FacilityTemplate { Id = FacilityTemplateCatalog.CernerId, Name = "Cerner" };
        var catalog = Catalog(Entry("local", "https://ehr.example/fhir", null, true, "Cerner"));
        var request = new StartScenarioRequest();
        var options = FacilityOptions(FacilityTemplateCatalog.SystemDefaultId);

        var tagged = await EhrSystemRunSelector.ApplyAsync(
            options, request, catalog, new TemplateStore(cerner), CancellationToken.None);

        tagged.EhrSystemName.Should().Be("local");
        tagged.FacilityTemplateId.Should().Be(cerner.Id);
        tagged.VendorName.Should().BeNull();
    }

    [Fact]
    public async Task A_missing_ehr_or_template_fails_the_start()
    {
        var catalog = Catalog(Entry("local", "https://ehr.example/fhir", null, true, "Missing"));
        var missingEhr = () => EhrSystemRunSelector.ApplyAsync(
            FacilityOptions(null),
            new StartScenarioRequest { EhrSystem = "other" },
            catalog,
            new TemplateStore(),
            CancellationToken.None);
        await missingEhr.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");

        var missingTemplate = () => EhrSystemRunSelector.ApplyAsync(
            FacilityOptions(FacilityTemplateCatalog.SystemDefaultId),
            new StartScenarioRequest(),
            catalog,
            new TemplateStore(),
            CancellationToken.None);
        await missingTemplate.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task Ala_carte_keeps_its_template_empty_and_still_records_the_ehr()
    {
        var catalog = Catalog(Entry("local", "https://ehr.example/fhir", null, true, FacilityTemplateCatalog.EpicId.ToString()));
        var options = FacilityOptions(null) with { FacilityConfigurationMode = FacilityConfigurationMode.AlaCarte };

        var tagged = await EhrSystemRunSelector.ApplyAsync(
            options,
            new StartScenarioRequest { RunConfigurationJson = """{"ehrSystem":"local"}""" },
            catalog,
            new TemplateStore(),
            CancellationToken.None);

        tagged.EhrSystemName.Should().Be("local");
        tagged.FacilityTemplateId.Should().BeNull();
    }

    [Fact]
    public void The_run_summary_update_stores_the_ehr_name()
    {
        var summary = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            RunName = "tagged",
            Scenario = AutomationScenarioKind.Custom,
            Status = AutomationRunStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            FacilityId = "facility",
            ReportId = "report",
            EhrSystem = "sandbox"
        };

        var rendered = Render(MongoSnapshotStore.BuildRunSummaryUpdate(summary, summary.FacilityId, summary.ReportId));
        rendered["$set"]["EhrSystem"].AsString.Should().Be("sandbox");
    }

    [Fact]
    public async Task Readiness_probes_that_systems_metadata_and_does_not_echo_secrets()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent("token=super-secret");
            return response;
        });
        var system = new NamedEhrSystem(
            "local",
            "https://ehr.example/fhir",
            "http://fhir-server:8080/fhir",
            null,
            true,
            new OAuthConfig(),
            new BasicAuthConfig { ShouldAuthenticate = true, Username = "reader", Password = "super-secret" });

        var result = await EhrSystemReadinessChecker.CheckAsync(system, new HttpClient(handler), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsoluteUri.Should().Be("https://ehr.example/fhir/metadata");
        captured.Headers.Authorization!.Scheme.Should().Be("Basic");
        result.Ready.Should().BeTrue();
        result.FhirServerBase.Should().Be("https://ehr.example/fhir");
        result.Error.Should().BeNull();
        JsonSerializer.Serialize(result).Should().NotContain("super-secret");
    }

    [Fact]
    public async Task A_failed_probe_reports_the_status_and_not_the_body()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Content = new StringContent("access_token=super-secret");
            return response;
        });
        var system = EntrySystem("local", "https://ehr.example/fhir");

        var result = await EhrSystemReadinessChecker.CheckAsync(system, new HttpClient(handler), CancellationToken.None);

        result.Ready.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Contain("401");
        result.Error.Should().NotContain("super-secret");
    }

    [Fact]
    public async Task A_silent_token_endpoint_fails_the_probe_within_the_timeout()
    {
        using var listener = Hang();
        var system = OauthSystem(listener);
        var started = Stopwatch.StartNew();

        var result = await EhrSystemReadinessChecker.CheckAsync(
            system,
            new HttpClient(new StubHandler(_ => throw new InvalidOperationException("metadata was called"))),
            CancellationToken.None,
            tokenTimeout: TimeSpan.FromSeconds(1));

        started.Stop();
        result.Ready.Should().BeFalse();
        result.Error.Should().NotContain("not-a-real-password");
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task Cancelling_the_probe_stops_the_token_call()
    {
        using var listener = Hang();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var started = Stopwatch.StartNew();

        var act = async () => await EhrSystemReadinessChecker.CheckAsync(
            OauthSystem(listener),
            new HttpClient(new StubHandler(_ => throw new InvalidOperationException("metadata was called"))),
            cts.Token,
            tokenTimeout: TimeSpan.FromSeconds(30));

        await act.Should().ThrowAsync<OperationCanceledException>();
        started.Stop();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Committed_link_ui_settings_name_the_local_endpoint_and_hold_no_secrets()
    {
        var root = RepoRoot();
        var baseSettings = File.ReadAllText(Path.Combine(root, "DotNet", "Link.UI", "appsettings.json"));
        baseSettings.Should().NotContain("EhrSystems");

        foreach (var fileName in new[] { "appsettings.Development.json", "appsettings.Docker.json" })
        {
            var path = Path.Combine(root, "DotNet", "Link.UI", fileName);
            var text = File.ReadAllText(path);
            text.Should().NotContain("ClientSecret");
            text.Should().NotContain("Password");

            var config = new ConfigurationBuilder().AddJsonFile(path).Build();
            var systems = EhrSystemCatalog.Parse(config, new AutomationConfig());
            systems.Should().ContainSingle();
            systems[0].Name.Should().Be("local");
            systems[0].OAuth.ClientSecret.Should().BeNull();
            systems[0].OAuth.Password.Should().BeNull();
            systems[0].BasicAuth.Password.Should().BeNull();
            systems[0].FhirServerBase.Should().StartWith("http");
        }
    }

    [Fact]
    public void The_run_page_shows_the_ehr_the_run_was_tagged_with()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "DotNet", "Link.UI", "Views", "Automation", "_RunDetail.cshtml"));
        page.Should().Contain("Model.EhrSystem");
        page.Should().Contain("runEhrSystem");
    }

    private static EhrSystemEntry OauthEntry(
        string? tokenEndpoint = "https://auth.example/token",
        string? clientId = "client",
        string? username = "reader",
        string? password = "not-a-real-password") => new() // trufflehog:ignore
    {
        Name = "local",
        FhirServerBase = "https://ehr.example/fhir",
        IsDefault = true,
        OAuth = new OAuthConfig
        {
            ShouldAuthenticate = true,
            TokenEndpoint = tokenEndpoint,
            ClientId = clientId,
            Username = username,
            Password = password
        }
    };

    private static NamedEhrSystem OauthSystem(TcpListener listener)
    {
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return new NamedEhrSystem(
            "local",
            "https://ehr.example/fhir",
            "https://ehr.example/fhir",
            null,
            true,
            new OAuthConfig
            {
                ShouldAuthenticate = true,
                TokenEndpoint = $"http://127.0.0.1:{port}/token",
                ClientId = "client",
                Username = "reader",
                Password = "not-a-real-password" // trufflehog:ignore
            },
            new BasicAuthConfig());
    }

    private static TcpListener Hang()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static EhrSystemEntry Entry(string name, string url, string? facilityBase, bool isDefault, string? template = null) => new()
    {
        Name = name,
        FhirServerBase = url,
        FacilityFhirServerBase = facilityBase,
        IsDefault = isDefault,
        DefaultFacilityTemplate = template
    };

    private static NamedEhrSystem EntrySystem(string name, string url) =>
        EhrSystemCatalog.Validate([Entry(name, url, null, true)])[0];

    private static EhrSystemCatalog Catalog(params EhrSystemEntry[] entries)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var prefix = $"Automation:EhrSystems:{index}";
            values[$"{prefix}:Name"] = entry.Name;
            values[$"{prefix}:FhirServerBase"] = entry.FhirServerBase;
            values[$"{prefix}:FacilityFhirServerBase"] = entry.FacilityFhirServerBase;
            values[$"{prefix}:IsDefault"] = entry.IsDefault ? "true" : "false";
            values[$"{prefix}:DefaultFacilityTemplate"] = entry.DefaultFacilityTemplate;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new EhrSystemCatalog(configuration, new FixedOptions(new AutomationConfig()));
    }

    private static ResolvedRunOptions FacilityOptions(Guid? templateId) =>
        new(1, 1, 1, 3, 30, 30, false, true, [], [], [])
        {
            FacilityConfigurationMode = FacilityConfigurationMode.Facility,
            FacilityTemplateId = templateId
        };

    private static BsonValue Render(UpdateDefinition<AutomationRunDocument> update)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<AutomationRunDocument>();
        return update.Render(new RenderArgs<AutomationRunDocument>(serializer, registry));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private sealed class FixedOptions(AutomationConfig value) : IOptionsMonitor<AutomationConfig>
    {
        public AutomationConfig CurrentValue { get; } = value;
        public AutomationConfig Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AutomationConfig, string?> listener) => null;
    }

    private sealed class TemplateStore(params FacilityTemplate[] templates) : IFacilityTemplateStore
    {
        public Task<List<FacilityTemplate>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(templates.ToList());

        public Task<FacilityTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<FacilityTemplate?> GetDefaultAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpsertAsync(FacilityTemplate template, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SetDefaultAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
