using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Local seed data. Link does not seed reference data through migrations; each environment is seeded
/// separately. This is that step for the AppHost: it runs once Account turns healthy, and again from the
/// <b>Seed data</b> command. Every step goes through the owning service's own API, only creates what is
/// missing and never changes what exists, so running it again is harmless.
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

    /// <summary>Seeds Account when it turns healthy, and adds a <b>Seed data</b> command to run it again.</summary>
    public static IResourceBuilder<ProjectResource> WithSeedData(this IResourceBuilder<ProjectResource> account)
    {
        account.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(account.Resource, async (ready, cancellationToken) =>
        {
            var logger = ready.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.SeedData");
            try
            {
                await SeedAsync(logger, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                // Don't fail the run over seed data. The command can be used once the cause is fixed.
                logger.LogError("Seeding Account failed: {Error}. Run the Seed data command on account to try again.", ex.Message);
            }
        });

        return account.WithCommand(
            name: "seed-data",
            displayName: "Seed data",
            executeCommand: async context =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.SeedData");
                try
                {
                    var summary = await SeedAsync(logger, context.CancellationToken);
                    return CommandResults.Success(summary);
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
                {
                    logger.LogError("Seeding Account failed: {Error}", ex.Message);
                    return CommandResults.Failure("Seeding Account failed. The AppHost console has the details.");
                }
            },
            commandOptions: new CommandOptions
            {
                Description = "Create the local seed data that is missing (Account roles)",
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "DatabaseLightning",
                IconVariant = IconVariant.Filled
            });
    }

    /// <summary>Runs every seed step and returns a one-line summary of what it created.</summary>
    public static async Task<string> SeedAsync(ILogger logger, CancellationToken cancellationToken)
    {
        using var http = AccountClient();
        var created = await EnsureRolesAsync(http, logger, cancellationToken);
        return created.Count == 0
            ? "Seed data is already in place."
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

    /// <summary>
    /// A client for the Account service, carrying a Link system token. Under anonymous access Account ignores
    /// it; when the services require authentication (Auth:Mode Keycloak) it is what lets these calls through.
    /// </summary>
    public static HttpClient AccountClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(DotNetServiceExtensions.Url(Ports.Account)), Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", LinkSystemToken.Create());
        return client;
    }
}
