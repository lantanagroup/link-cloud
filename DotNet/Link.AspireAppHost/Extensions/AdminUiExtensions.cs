using System.Text.Json;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Runs the Admin UI under <c>ng serve</c>, proxying <c>/api</c> to the Admin BFF, as developers run it
/// on its own. Only the proxy file differs: <c>AdminUi/proxy.mjs</c> reads the BFF's
/// address from the environment, and the UI's own <c>proxy.conf.json</c> is not touched.
/// </summary>
internal static class AdminUiExtensions
{
    /// <param name="builder">The AppHost.</param>
    /// <param name="adminBff">The Admin BFF, which <c>/api</c> is proxied to.</param>
    /// <param name="authRequired">
    /// Whether the UI asks people to sign in. True when Keycloak signs people in to the Admin BFF. Under
    /// anonymous access the Admin BFF maps no login endpoint, so a sign-in page would 404.
    /// </param>
    public static IResourceBuilder<ExecutableResource> AddAdminUi(
        this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> adminBff, bool authRequired)
    {
        var uiDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "Web", "Admin.UI"));
        var proxyConfig = Path.Combine(builder.AppHostDirectory, "AdminUi", "proxy.mjs");

        // Merged over src/assets/app.config.json by the UI (see AdminUi/proxy.mjs).
        var uiConfig = JsonSerializer.Serialize(new { authRequired });

        var ui = builder.AddExecutable(
                "admin-ui", "node", uiDirectory,
                Path.Combine("node_modules", "@angular", "cli", "bin", "ng.js"),
                "serve", "--port", Ports.AdminUi.ToString(), "--proxy-config", proxyConfig)
            .WithHttpEndpoint(port: Ports.AdminUi, isProxied: false)
            .WithEnvironment("LINK_ADMIN_BFF_URL", DotNetServiceExtensions.Url(Ports.AdminBff))
            .WithEnvironment("LINK_ADMIN_UI_CONFIG", uiConfig)
            .WaitFor(adminBff);

        // Install the packages only when they are missing, so a start does not wipe node_modules the
        // way npm ci would. Delete node_modules to have the next start install them again.
        if (!Directory.Exists(Path.Combine(uiDirectory, "node_modules", "@angular", "cli")))
        {
            var install = OperatingSystem.IsWindows()
                ? builder.AddExecutable("admin-ui-install", "cmd", uiDirectory, "/c", "npm", "ci")
                : builder.AddExecutable("admin-ui-install", "npm", uiDirectory, "ci");
            ui.WaitForCompletion(install);
        }

        return ui;
    }
}
