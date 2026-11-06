using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Link.AspireAppHost;

/// <summary>
/// Runs the Admin UI under <c>ng serve</c>, proxying <c>/api</c> to the Admin BFF, as developers run it
/// on its own. Only the proxy file differs: <c>admin-ui.proxy.mjs</c> in this folder reads the BFF's
/// address from the environment, and the UI's own <c>proxy.conf.json</c> is not touched.
/// </summary>
internal static class AdminUiExtensions
{
    public static IResourceBuilder<ExecutableResource> AddAdminUi(
        this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> adminBff)
    {
        var uiDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "Web", "Admin.UI"));
        var proxyConfig = Path.Combine(builder.AppHostDirectory, "admin-ui.proxy.mjs");

        // Merged over src/assets/app.config.json by the UI (see admin-ui.proxy.mjs). The services run with
        // anonymous access, and the Admin BFF does not map its login endpoints then, so the UI must not
        // send people to a login page that would 404. Set AdminUi:AuthRequired to true once local
        // authentication is configured.
        var uiConfig = JsonSerializer.Serialize(new
        {
            authRequired = builder.Configuration.GetValue("AdminUi:AuthRequired", false)
        });

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
