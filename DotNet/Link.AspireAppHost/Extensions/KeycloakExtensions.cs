using System.Diagnostics;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.Configuration;

namespace Link.AspireAppHost.Extensions;

internal static class KeycloakExtensions
{
    private const string Image = "quay.io/keycloak/keycloak";
    private const string Tag = "26.5";

    public static AuthMode GetAuthMode(this IConfiguration configuration) =>
        Enum.TryParse<AuthMode>(configuration["Auth:Mode"], ignoreCase: true, out var mode) ? mode : AuthMode.Anonymous;

    /// <summary>
    /// Keycloak in development mode, serving HTTPS with the .NET development certificate and importing the
    /// <c>link</c> realm from <c>Keycloak/link-realm.json</c>.
    /// </summary>
    /// <remarks>
    /// HTTPS is needed because the Admin BFF requires an HTTPS authority outside Development
    /// (OpenIdConnectSchemeExtension: <c>RequireHttpsMetadata = !IsDevelopment()</c>), and it runs here in the
    /// Docker environment. The development certificate is trusted on the machine, so the Admin BFF, this
    /// AppHost and the browser all accept it. Keycloak's data is kept in a volume, so users survive restarts;
    /// the realm is imported only when it does not exist yet.
    /// </remarks>
    public static LinkKeycloak AddLinkKeycloak(this IDistributedApplicationBuilder builder)
    {
        // Generated on the first run and kept in this AppHost's user secrets. Letters and digits only: the
        // client secret is substituted into the realm file's JSON.
        var adminPassword = builder.AddParameter("keycloak-admin-password",
            new GenerateParameterDefault { MinLength = 24, Special = false }, secret: true, persist: true);
        var clientSecret = builder.AddParameter("keycloak-client-secret",
            new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);

        var certificates = ExportDevelopmentCertificate();
        var realmFile = Path.Combine(builder.AppHostDirectory, "Keycloak", "link-realm.json");

        var keycloak = builder.AddContainer("keycloak", Image, Tag)
            .WithArgs("start-dev", "--import-realm")
            .WithHttpsEndpoint(port: Ports.Keycloak, targetPort: 8443, name: "https", isProxied: false)
            .WithBindMount(certificates, "/opt/keycloak/conf/certs", isReadOnly: true)
            .WithBindMount(realmFile, "/opt/keycloak/data/import/link-realm.json", isReadOnly: true)
            .WithVolume("link-aspire-keycloak", "/opt/keycloak/data")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", LinkKeycloak.AdminUser)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", adminPassword)
            .WithEnvironment("KC_HTTPS_CERTIFICATE_FILE", "/opt/keycloak/conf/certs/tls.crt")
            .WithEnvironment("KC_HTTPS_CERTIFICATE_KEY_FILE", "/opt/keycloak/conf/certs/tls.key")
            .WithEnvironment("KC_HTTP_ENABLED", "false")
            .WithEnvironment("KC_HOSTNAME", LinkKeycloak.BaseUrl)
            // Read by the realm file's ${LINK_BFF_CLIENT_SECRET} when the realm is imported.
            .WithEnvironment("LINK_BFF_CLIENT_SECRET", clientSecret)
            .WithHttpHealthCheck($"/realms/{LinkKeycloak.Realm}/.well-known/openid-configuration", endpointName: "https");

        return new LinkKeycloak(keycloak, adminPassword, clientSecret);
    }

    /// <summary>
    /// Exports the .NET development certificate and its key as PEM files for Keycloak, into a folder under
    /// the temp directory, and returns the folder. The certificate is the one <c>dotnet dev-certs https</c>
    /// manages; trust it once with <c>dotnet dev-certs https --trust</c>.
    /// </summary>
    private static string ExportDevelopmentCertificate()
    {
        var folder = Path.Combine(Path.GetTempPath(), "link-aspire", "keycloak-certs");
        Directory.CreateDirectory(folder);
        var certificate = Path.Combine(folder, "tls.crt");

        using var export = Process.Start(new ProcessStartInfo("dotnet",
            ["dev-certs", "https", "--export-path", certificate, "--format", "Pem", "--no-password"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start dotnet dev-certs.");
        var output = export.StandardOutput.ReadToEnd() + export.StandardError.ReadToEnd();
        export.WaitForExit();

        if (export.ExitCode != 0 || !File.Exists(certificate) || !File.Exists(Path.Combine(folder, "tls.key")))
            throw new InvalidOperationException(
                "Could not export the .NET development certificate for Keycloak. Run 'dotnet dev-certs https --trust' and try again. " + output.Trim());
        return folder;
    }
}
