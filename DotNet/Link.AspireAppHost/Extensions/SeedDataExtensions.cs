using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Local seed data. Link does not seed reference data through migrations; each environment is seeded
/// separately. This is that step for the AppHost. It has three parts, and each runs when its own service
/// turns healthy:
/// <list type="bullet">
/// <item><b>Roles</b> (Account): the roles a user can be given.</item>
/// <item><b>Validation</b>: its artifacts and categories, initialised from the files it ships with, and the rubric in
/// <c>SeedData/Validation</c>, registered and published.</item>
/// <item><b>Measures</b> (MeasureEval): the measure bundles in <c>DotNet/Automation/measures</c>, with their value sets
/// expanded by Terminology. Tenant refuses
/// a facility that schedules a measure MeasureEval does not have.</item>
/// </list>
/// The <b>Seed data</b> command on the Admin BFF runs all three again. Every part goes through the owning service's
/// own API, only creates what is missing and never changes what exists, so running it again is harmless.
/// </summary>
internal static class SeedDataExtensions
{
    /// <summary>A role Account should have, with the permissions it carries.</summary>
    private sealed record SeedRole(string Name, string Description, string[] Permissions);

    /// <summary>
    /// The roles a local Account needs. LinkUser is the one Account's own (unused) SeedRoles describes.
    /// Add further roles here.
    /// </summary>
    private static readonly SeedRole[] Roles =
    [
        new("LinkUser", "A user of the Link application", ["IsLinkAdmin"])
    ];

    /// <summary>The bundle entries MeasureEval stores with a measure definition, as Automation's MeasureLoader sends them.</summary>
    private static readonly HashSet<string> MeasureResourceTypes = ["Measure", "Library", "ValueSet", "CodeSystem"];

    /// <summary>
    /// Seeds each service when it turns healthy, and adds a <b>Seed data</b> command to <paramref name="host"/> (the
    /// Admin BFF) that runs every part again. The command spans several services, so it sits on the front door
    /// rather than on any one of them.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithSeedDataCommand(
        this IResourceBuilder<ProjectResource> host,
        IResourceBuilder<ProjectResource> account,
        IResourceBuilder<ExecutableResource> measureEval,
        IResourceBuilder<ExecutableResource> validation)
    {
        var measuresDirectory = Path.GetFullPath(Path.Combine(host.ApplicationBuilder.AppHostDirectory, "..", "Automation", "measures"));
        var rubricFile = RubricFile(host.ApplicationBuilder.AppHostDirectory);

        SeedWhenReady(account, "roles", (logger, cancellationToken) => SeedRolesAsync(logger, cancellationToken));
        SeedWhenReady(validation, "validation", (logger, cancellationToken) => SeedValidationAsync(rubricFile, logger, cancellationToken));
        SeedWhenReady(measureEval, "measures", (logger, cancellationToken) => SeedMeasuresAsync(measuresDirectory, logger, cancellationToken));

        return host.WithCommand(
            name: "seed-data",
            displayName: "Seed data",
            executeCommand: context => CommandNotifications.RunWithNotificationsAsync(context, "Seed data",
                "Seeding Account roles, Validation artifacts, categories and rubric, and measures. Loading measures can take a minute.", async () =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.SeedData");
                var parts = new (string Name, Func<Task<string>> Run)[]
                {
                    ("roles", () => SeedRolesAsync(logger, context.CancellationToken)),
                    ("validation", () => SeedValidationAsync(rubricFile, logger, context.CancellationToken)),
                    ("measures", () => SeedMeasuresAsync(measuresDirectory, logger, context.CancellationToken))
                };

                // Each part runs whatever happened to the others, so one service being down does not stop the rest.
                var results = new List<string>();
                var failed = false;
                foreach (var (name, run) in parts)
                {
                    try
                    {
                        results.Add(await run());
                    }
                    catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
                    {
                        failed = true;
                        logger.LogError("Seeding {Part} failed: {Error}", name, ex.Message);
                        results.Add($"Seeding {name} failed; the AppHost console has the details.");
                    }
                }

                var summary = string.Join(" ", results);
                return failed ? CommandResults.Failure(summary) : CommandResults.Success(summary);
            }),
            commandOptions: new CommandOptions
            {
                Description = "Create the local seed data that is missing: Account roles, Validation artifacts, categories and rubric, measures",
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "DatabaseLightning",
                IconVariant = IconVariant.Filled
            });
    }

    /// <summary>Runs one seed part when <paramref name="resource"/> turns healthy. A failure is logged, not thrown.</summary>
    private static void SeedWhenReady<T>(IResourceBuilder<T> resource, string part, Func<ILogger, CancellationToken, Task<string>> seed)
        where T : IResource =>
        resource.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(resource.Resource, async (ready, cancellationToken) =>
        {
            var logger = ready.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.SeedData");
            try
            {
                logger.LogInformation("Seed data ({Part}): {Summary}", part, await seed(logger, cancellationToken));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
            {
                // Don't fail the run over seed data. The command can be used once the cause is fixed.
                logger.LogError("Seeding {Part} failed: {Error}. Run the Seed data command on admin-bff to try again.", part, ex.Message);
            }
        });

    // ---------------------------------------------------------------------------------------------------
    // Roles
    // ---------------------------------------------------------------------------------------------------

    private static async Task<string> SeedRolesAsync(ILogger logger, CancellationToken cancellationToken)
    {
        using var http = AccountClient();
        var created = await EnsureRolesAsync(http, logger, cancellationToken);
        return created.Count == 0
            ? "The Account roles are in place."
            : $"Created the Account roles {string.Join(", ", created)}.";
    }

    /// <summary>Creates each role in <see cref="Roles"/> that Account does not have. Returns the names created.</summary>
    public static async Task<IReadOnlyList<string>> EnsureRolesAsync(HttpClient http, ILogger logger, CancellationToken cancellationToken)
    {
        var existing = (await GetRoleNamesAsync(http, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var created = new List<string>();
        foreach (var role in Roles.Where(r => !existing.Contains(r.Name)))
        {
            using var response = await http.PostAsJsonAsync(
                "api/account/role",
                new { name = role.Name, description = role.Description, claims = role.Permissions },
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.Conflict)
                continue; // Created between the list and the create, by another run.
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Account refused the role {role.Name} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");

            logger.LogInformation("Seeded the Account role {Role} with {Permissions}", role.Name, string.Join(", ", role.Permissions));
            created.Add(role.Name);
        }
        return created;
    }

    /// <summary>The names of the roles Account has, from <c>GET /api/account/role</c>.</summary>
    public static async Task<IReadOnlyList<string>> GetRoleNamesAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await http.GetStringAsync("api/account/role", cancellationToken));
        return document.RootElement.ValueKind is JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
                .Select(r => r.TryGetProperty("name", out var name) ? name.GetString() : null)
                .OfType<string>()
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
    }

    /// <summary>A client for the Account service, carrying a Link system token.</summary>
    public static HttpClient AccountClient() => LinkServiceClient.Create(Ports.Account);

    // ---------------------------------------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The rubric the shadow comparison (and the VaaS bridge, when enabled) evaluates against.</summary>
    public static string RubricFile(string appHostDirectory) =>
        Path.Combine(appHostDirectory, "SeedData", "Validation", "bare-minimum-requirements.yaml");

    /// <summary>
    /// The rubric's <c>id</c> and <c>semver</c>, read from the top level of its YAML. Validation registers a version under
    /// the id in its path, which must match the file's.
    /// </summary>
    public static (string Id, string Semver) ReadRubricIdentity(string rubricFile)
    {
        string? id = null, semver = null;
        foreach (var line in File.ReadLines(rubricFile))
        {
            if (line.StartsWith("id:", StringComparison.Ordinal)) id = line[3..].Trim().Trim('\'', '"');
            else if (line.StartsWith("semver:", StringComparison.Ordinal)) semver = line[7..].Trim().Trim('\'', '"');
            if (id is not null && semver is not null) break;
        }
        return (id ?? throw new InvalidOperationException($"{Path.GetFileName(rubricFile)} has no top-level id."),
                semver ?? throw new InvalidOperationException($"{Path.GetFileName(rubricFile)} has no top-level semver."));
    }

    /// <summary>
    /// Initialises Validation's artifacts and categories from the files it ships with, each only when Validation has
    /// none yet, as Automation does (ValidationApiHelper). Then registers and publishes the rubric in
    /// <paramref name="rubricFile"/> if that version is missing or still a draft. A published version cannot change,
    /// so a rubric that changes needs a new semver.
    /// </summary>
    private static async Task<string> SeedValidationAsync(string rubricFile, ILogger logger, CancellationToken cancellationToken)
    {
        using var http = LinkServiceClient.Create(Ports.Validation, TimeSpan.FromMinutes(5));
        var done = new List<string>();
        foreach (var (name, path) in new[] { ("artifacts", "api/validation/artifact"), ("categories", "api/validation/category") })
        {
            using var list = await http.GetAsync(path, cancellationToken);
            list.EnsureSuccessStatusCode();
            var body = await list.Content.ReadAsStringAsync(cancellationToken);
            if (HasContent(body))
                continue;

            using var initialise = await http.PostAsync($"{path}/$initialize", content: null, cancellationToken);
            if (!initialise.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Validation refused to initialise its {name} ({(int)initialise.StatusCode}): {await initialise.Content.ReadAsStringAsync(cancellationToken)}");

            logger.LogInformation("Initialised Validation's {Part}", name);
            done.Add($"initialised its {name}");
        }

        var (rubricId, semver) = ReadRubricIdentity(rubricFile);
        var versionPath = $"api/validation/rubrics/{Uri.EscapeDataString(rubricId)}/versions/{Uri.EscapeDataString(semver)}";
        var status = await GetRubricStatusAsync(http, versionPath, cancellationToken);
        if (status is null)
        {
            using var yaml = new StringContent(await File.ReadAllTextAsync(rubricFile, cancellationToken), Encoding.UTF8, "application/yaml");
            using var register = await http.PostAsync($"api/validation/rubrics/{Uri.EscapeDataString(rubricId)}/versions", yaml, cancellationToken);
            if (!register.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Validation refused the rubric {rubricId} {semver} ({(int)register.StatusCode}): {await register.Content.ReadAsStringAsync(cancellationToken)}");
            logger.LogInformation("Registered the rubric {Rubric} {Semver}", rubricId, semver);
            done.Add($"registered the rubric {rubricId} {semver}");
            status = "DRAFT";
        }
        if (string.Equals(status, "DRAFT", StringComparison.OrdinalIgnoreCase))
        {
            using var publish = await http.PostAsync($"{versionPath}/$publish", content: null, cancellationToken);
            if (!publish.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Validation refused to publish the rubric {rubricId} {semver} ({(int)publish.StatusCode}): {await publish.Content.ReadAsStringAsync(cancellationToken)}");
            logger.LogInformation("Published the rubric {Rubric} {Semver}", rubricId, semver);
            done.Add("published it");
        }

        return done.Count == 0
            ? $"Validation's artifacts, categories and rubric {rubricId} {semver} are in place."
            : $"Validation: {string.Join(", ", done)}.";
    }

    /// <summary>The status of a rubric version (DRAFT, PUBLISHED or RETIRED), or null when it is not registered.</summary>
    private static async Task<string?> GetRubricStatusAsync(HttpClient http, string versionPath, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(versionPath, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Checking the rubric version answered {(int)response.StatusCode}: {body}");

        // The version sits in an ApiResponse envelope, under "data".
        var root = JsonNode.Parse(body);
        var data = root?["data"] ?? root;
        return data?["status"]?.GetValue<string>();
    }

    // ---------------------------------------------------------------------------------------------------
    // Measures
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Loads each measure bundle in <paramref name="directory"/> into MeasureEval. A bundle's id is the measure id; it is
    /// what facilities schedule and ad-hoc reports name.
    /// </summary>
    /// <remarks>
    /// Each value set the bundle carries without an expansion is expanded first by the local Terminology service, as the
    /// QA Postman collection does when it loads a measure. Without an expansion MeasureEval falls back to the value set's
    /// compose definition and warns that results may be wrong. A measure already loaded is loaded again only when its
    /// stored value sets have fewer expansions than the bundle now has, which brings an older local database up to date.
    /// If Terminology is not answering, the bundle is loaded as it is and the summary says so.
    /// </remarks>
    private static async Task<string> SeedMeasuresAsync(string directory, ILogger logger, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"The measure bundles were not found in {directory}.");

        // Compiling a measure's CQL can take a while.
        using var http = LinkServiceClient.Create(Ports.MeasureEval, TimeSpan.FromMinutes(5));
        using var terminology = LinkServiceClient.Create(Ports.Terminology, TimeSpan.FromMinutes(2));
        var terminologyUp = await WaitForTerminologyAsync(terminology, cancellationToken);

        var loaded = new List<string>();
        var present = new List<string>();
        foreach (var file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            // The files are saved with a byte-order mark, which JsonNode.Parse does not accept.
            var bundle = JsonNode.Parse((await File.ReadAllTextAsync(file, cancellationToken)).TrimStart('﻿'))?.AsObject()
                ?? throw new InvalidOperationException($"{Path.GetFileName(file)} is not a JSON object.");
            var id = bundle["id"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"{Path.GetFileName(file)} has no Bundle.id.");

            // Only the entries MeasureEval keeps with a definition, as Automation's MeasureLoader sends them.
            if (bundle["entry"] is JsonArray entries)
            {
                foreach (var entry in entries.ToList())
                {
                    var type = entry?["resource"]?["resourceType"]?.GetValue<string>();
                    if (type is null || !MeasureResourceTypes.Contains(type))
                        entries.Remove(entry);
                }
            }

            var expanded = terminologyUp ? await ExpandValueSetsAsync(terminology, bundle, id, logger, cancellationToken) : 0;
            var wanted = CountExpandedValueSets(bundle);

            using var existing = await http.GetAsync($"api/measureeval/measure-definition/{Uri.EscapeDataString(id)}", cancellationToken);
            if (existing.StatusCode is HttpStatusCode.OK)
            {
                var stored = JsonNode.Parse(await existing.Content.ReadAsStringAsync(cancellationToken))?["bundle"];
                if (CountExpandedValueSets(stored) >= wanted)
                {
                    present.Add(id);
                    continue;
                }
                logger.LogInformation("Reloading the measure {Measure}: it is stored with fewer expanded value sets than the bundle now has", id);
            }

            using var content = new StringContent(bundle.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await http.PutAsync("api/measureeval/measure-definition", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"MeasureEval refused the measure {id} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");

            logger.LogInformation("Loaded the measure {Measure} into MeasureEval ({Expanded} value sets expanded by Terminology)", id, expanded);
            loaded.Add(id);
        }

        var summary = loaded.Count == 0
            ? $"The measures are in place ({string.Join(", ", present)})."
            : $"Loaded the measures {string.Join(", ", loaded)}.";
        return terminologyUp ? summary : summary + " Terminology was not answering, so their value sets were not expanded.";
    }

    /// <summary>Waits up to a minute for Terminology to answer, since MeasureEval can be ready first.</summary>
    private static async Task<bool> WaitForTerminologyAsync(HttpClient terminology, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                using var health = await terminology.GetAsync("health", cancellationToken);
                if (health.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
                // Not up yet.
            }
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        return false;
    }

    /// <summary>
    /// Replaces each value set in the bundle that has no expansion with Terminology's expansion of it, paging through
    /// <c>$expand</c> (at most 5000 codes a page) until every code is in. Returns how many were expanded. A value set
    /// Terminology does not know is left as it is.
    /// </summary>
    private static async Task<int> ExpandValueSetsAsync(HttpClient terminology, JsonObject bundle, string measureId, ILogger logger, CancellationToken cancellationToken)
    {
        const int pageSize = 5000;
        var count = 0;
        var resources = (bundle["entry"] as JsonArray ?? []).Select(e => e?["resource"] as JsonObject).OfType<JsonObject>().ToList();
        foreach (var resource in resources)
        {
            if (resource["resourceType"]?.GetValue<string>() != "ValueSet" || resource["expansion"] is not null)
                continue;
            var valueSetId = resource["id"]?.GetValue<string>() ?? resource["url"]?.GetValue<string>()?.Split('/').Last();
            if (string.IsNullOrEmpty(valueSetId))
                continue;

            var contains = new JsonArray();
            JsonObject? expansion = null;
            for (var offset = 0; ; offset += pageSize)
            {
                using var response = await terminology.GetAsync(
                    $"api/terminology/fhir/ValueSet/{Uri.EscapeDataString(valueSetId)}/$expand?count={pageSize}&offset={offset}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Terminology could not expand the value set {ValueSet} for {Measure} ({Status}); leaving it as it is",
                        valueSetId, measureId, (int)response.StatusCode);
                    expansion = null;
                    break;
                }
                var page = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?["expansion"] as JsonObject;
                if (page is null)
                    break;
                expansion ??= page.DeepClone().AsObject();
                var pageContains = page["contains"] as JsonArray ?? [];
                foreach (var code in pageContains)
                    contains.Add(code?.DeepClone());
                var total = page["total"]?.GetValue<int>() ?? contains.Count;
                if (pageContains.Count == 0 || contains.Count >= total)
                    break;
            }

            if (expansion is null || contains.Count == 0)
                continue;
            expansion["contains"] = contains;
            expansion["total"] = contains.Count;
            expansion.Remove("offset");
            expansion.Remove("parameter");
            resource["expansion"] = expansion;
            count++;
        }
        return count;
    }

    /// <summary>How many ValueSet entries in a bundle carry an expansion with codes.</summary>
    private static int CountExpandedValueSets(JsonNode? bundle) =>
        (bundle?["entry"] as JsonArray ?? [])
            .Select(e => e?["resource"])
            .Count(r => r?["resourceType"]?.GetValue<string>() == "ValueSet" && (r["expansion"]?["contains"] as JsonArray)?.Count > 0);

    /// <summary>
    /// True when a response body holds something. A paged search result (<c>{"records": [...], "metadata": {...}}</c>,
    /// the shape Link's search endpoints return) holds something only when <c>records</c> has items; it is a non-empty
    /// object even when nothing matched. Otherwise: a non-empty array, an object with properties, or any other value.
    /// </summary>
    internal static bool HasContent(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.Array => root.GetArrayLength() > 0,
                JsonValueKind.Object when TryGetRecords(root, out var records) => records.GetArrayLength() > 0,
                JsonValueKind.Object => root.EnumerateObject().Any(),
                JsonValueKind.Null or JsonValueKind.Undefined => false,
                _ => true
            };
        }
        catch (JsonException)
        {
            return true; // Not JSON, but something came back.
        }
    }

    private static bool TryGetRecords(JsonElement root, out JsonElement records)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, "records", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind is JsonValueKind.Array)
            {
                records = property.Value;
                return true;
            }
        }
        records = default;
        return false;
    }
}
