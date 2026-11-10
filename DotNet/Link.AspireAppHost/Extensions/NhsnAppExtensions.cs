using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Link.AspireAppHost.Extensions.DotNetServiceExtensions;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// The NHSN App (<c>nhsn/</c>): its BFF, and the standalone shell of its UI that developers use as a
/// stand-in for the CDC NHSN App.
/// </summary>
/// <remarks>
/// In production the NHSN gateway signs a JWT for each request and the BFF validates it against the
/// gateway's certificate. Locally the UI's shell signs the JWT in the browser from a test profile, so the
/// BFF needs a certificate and the shell the matching private key. Compose carries a fixed pair in
/// <c>docker-compose.yml</c>; here a pair is made on the first run (see <see cref="NhsnAppJwt"/>).
/// </remarks>
internal static class NhsnAppExtensions
{
    /// <summary>
    /// Whether the NHSN App runs: <c>NhsnApp:Enabled</c>, false unless set otherwise, because its UI needs
    /// a package file each developer has to put in place first.
    /// </summary>
    public static bool NhsnAppEnabled(this IDistributedApplicationBuilder builder) =>
        builder.Configuration.GetValue("NhsnApp:Enabled", false);

    /// <summary>
    /// The NHSN App BFF, with the settings compose gives it. It calls the Link services directly, so it gets
    /// each one's address, and in Auth:Mode Keycloak it sends them Link bearer tokens.
    /// </summary>
    /// <param name="builder">The AppHost.</param>
    /// <param name="infrastructure">The shared containers.</param>
    /// <param name="jwt">The key pair the BFF validates the shell's tokens with.</param>
    /// <param name="requireToken">Whether the Link services require a Link bearer token.</param>
    /// <param name="keycloak">Keycloak, in Auth:Mode Keycloak.</param>
    public static IResourceBuilder<ProjectResource> AddNhsnAppBff(
        this IDistributedApplicationBuilder builder, LinkInfrastructure infrastructure, NhsnAppJwt jwt,
        bool requireToken, LinkKeycloak? keycloak)
    {
        (string Name, int Port)[] linkServices =
        [
            ("Census", Ports.Census),
            ("DataAcquisition", Ports.DataAcquisition),
            ("Normalization", Ports.Normalization),
            ("QueryDispatch", Ports.QueryDispatch),
            ("Report", Ports.Report),
            ("Measure", Ports.MeasureEval),
            ("Validation", Ports.Validation),
            ("Submission", Ports.Submission),
            ("AdminBff", Ports.AdminBff),
            ("Terminology", Ports.Terminology)
        ];

        var bff = builder.AddLinkService<Projects.NHSN_App_BFF>("nhsn-app-bff", Ports.NhsnAppBff)
            .WithDatabase(infrastructure.Database("nhsn-app-bff"))
            .WithLocalAuthentication(requireToken, keycloak)
            // This BFF's own switch for the calls it makes to the Link services. Its own endpoints always
            // require an NHSN JWT, whatever this is.
            .WithEnvironment("Authentication__AllowAnonymous", requireToken ? "false" : "true")
            .WithTenantService()
            .WithEnvironment("NhsnJwt__Issuer", NhsnAppJwt.Issuer)
            .WithEnvironment("NhsnJwt__PublicCertificatePem", jwt.CertificatePem)
            // Report's blobs, which the BFF reads with its own connection.
            .WithEnvironment("ReportBlobStorage__ConnectionString", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("CORS__AllowedOrigins__2", Url(Ports.NhsnAppUi))
            // Its appsettings.Docker.json lists the console sink first and Loki second, where the other
            // services list Loki first, so the address AddLinkService set goes on the second sink instead.
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables.Remove("Serilog__WriteTo__0__Args__uri");
                context.EnvironmentVariables["Serilog__WriteTo__1__Args__uri"] = LinkInfrastructure.LokiUrl;
            })
            .WaitFor(infrastructure.Azurite);

        foreach (var (name, port) in linkServices)
            bff.WithEnvironment($"ServiceRegistry__{name}ServiceUrl", Url(port));

        return bff;
    }

    /// <summary>
    /// Runs the NHSN App UI's standalone shell under <c>webpack serve</c>, as developers run it on its own
    /// with <c>npm start</c>. Only the webpack config differs: <c>NhsnAppUi/webpack.config.js</c> wraps the
    /// UI's own to proxy <c>/api</c> to this host's BFF and to hand the shell its test profile defaults.
    /// </summary>
    /// <remarks>
    /// The UI depends on the NHSN design system as a package file, <c>packages/nhsn-react-core-*.tgz</c>,
    /// which cannot be in the repository. <c>NhsnApp:ReactCorePackage</c> says where a copy is (see
    /// <see cref="EnsureReactCorePackage"/>). With no copy to install from, the UI is left out and a warning
    /// says why; the BFF is unaffected.
    /// </remarks>
    /// <returns>The UI, or null when it was left out.</returns>
    public static IResourceBuilder<ExecutableResource>? AddNhsnAppUi(
        this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> bff, NhsnAppJwt jwt)
    {
        var uiDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "nhsn", "NHSN-App-UI"));
        var webpackConfig = Path.Combine(builder.AppHostDirectory, "NhsnAppUi", "webpack.config.js");

        // Installed packages need nothing more. Otherwise npm ci needs the design system's package file.
        var installed = Directory.Exists(Path.Combine(uiDirectory, "node_modules", "webpack-cli"));
        if (!installed && EnsureReactCorePackage(builder, uiDirectory) is { } problem)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((start, _) =>
            {
                start.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(NhsnAppExtensions).FullName!)
                    .LogWarning("The NHSN App UI (nhsn-app-ui) is left out: {Problem}", problem);
                return Task.CompletedTask;
            });
            return null;
        }

        var ui = builder.AddExecutable(
                "nhsn-app-ui", "node", uiDirectory,
                Path.Combine("node_modules", "webpack-cli", "bin", "cli.js"),
                "serve", "--config", webpackConfig, "--mode", "development")
            .WithHttpEndpoint(port: Ports.NhsnAppUi, isProxied: false)
            .WithEnvironment("PORT", Ports.NhsnAppUi.ToString())
            .WithEnvironment("NHSN_APP_BFF_URL", Url(Ports.NhsnAppBff))
            // The names the UI's own server (server/main.js) reads, which compose sets.
            .WithEnvironment("NHSN_APP_UI_DEFAULT_JWT_ISSUER", NhsnAppJwt.Issuer)
            .WithEnvironment("NHSN_APP_UI_DEFAULT_JWT_KEY_ID", jwt.KeyId)
            .WithEnvironment("NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM", jwt.PrivateKeyPem)
            .WaitFor(bff);

        // Install the packages only when they are missing, as for the Admin UI.
        if (!installed)
        {
            var install = OperatingSystem.IsWindows()
                ? builder.AddExecutable("nhsn-app-ui-install", "cmd", uiDirectory, "/c", "npm", "ci")
                : builder.AddExecutable("nhsn-app-ui-install", "npm", uiDirectory, "ci");
            ui.WaitForCompletion(install);
        }

        return ui;
    }

    /// <summary>The setting that says where the NHSN design system's package file is.</summary>
    private const string ReactCorePackageSetting = "NhsnApp:ReactCorePackage";

    private const string ReactCorePackageName = "@nhsn/nhsn-react-core";

    /// <summary>
    /// Makes sure the package file the UI's <c>package.json</c> names is where npm looks for it:
    /// <c>nhsn/NHSN-App-UI/packages/</c>, which git ignores, so nothing is added to the repository.
    /// </summary>
    /// <remarks>
    /// <c>package.json</c> and the lock file name the file by a path inside the UI's folder, and
    /// <c>npm ci</c> installs only what the lock file says, so the file cannot be installed from where it
    /// is; it is copied in. <c>NhsnApp:ReactCorePackage</c> is the file, or a folder holding a file with
    /// the name <c>package.json</c> asks for. A copy already in <c>packages/</c> is used as it is when the
    /// setting is empty, and replaced when the configured file differs from it.
    /// </remarks>
    /// <returns>Null when the file is in place; otherwise what is missing, for the log.</returns>
    private static string? EnsureReactCorePackage(IDistributedApplicationBuilder builder, string uiDirectory)
    {
        using var packageJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(uiDirectory, "package.json")));
        var reference = packageJson.RootElement.GetProperty("dependencies").GetProperty(ReactCorePackageName).GetString() ?? "";
        if (!reference.StartsWith("file:", StringComparison.Ordinal))
            return null; // package.json no longer takes it from a file, so npm fetches it like any other.

        var target = Path.GetFullPath(Path.Combine(uiDirectory, reference["file:".Length..]));
        var fileName = Path.GetFileName(target);

        var configured = builder.Configuration[ReactCorePackageSetting];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(target)
                ? null
                : $"it needs {fileName}, which is not in the repository. Set {ReactCorePackageSetting} in this AppHost's user secrets to the file, or to the folder that holds it.";
        }

        var source = Path.GetFullPath(Directory.Exists(configured) ? Path.Combine(configured, fileName) : configured);
        if (!File.Exists(source))
            return $"{ReactCorePackageSetting} is \"{configured}\", and there is no {(Directory.Exists(configured) ? fileName + " in that folder" : "such file")}.";
        if (!string.Equals(Path.GetFileName(source), fileName, StringComparison.OrdinalIgnoreCase))
            return $"{ReactCorePackageSetting} names {Path.GetFileName(source)}, but the UI's package.json asks for {fileName}.";

        var sourceInfo = new FileInfo(source);
        var targetInfo = new FileInfo(target);
        if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase) &&
            (!targetInfo.Exists || targetInfo.Length != sourceInfo.Length || targetInfo.LastWriteTimeUtc != sourceInfo.LastWriteTimeUtc))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true); // Keeps the source's last write time.
        }

        return null;
    }
}

/// <summary>
/// The key pair that stands in for the NHSN gateway's: the BFF validates tokens against the certificate,
/// and the UI's shell signs them with the private key.
/// </summary>
/// <param name="CertificatePem">The self-signed certificate, for <c>NhsnJwt:PublicCertificatePem</c>.</param>
/// <param name="PrivateKeyPem">The PKCS#8 private key the shell signs ES256 tokens with.</param>
/// <param name="KeyId">The certificate's thumbprint, which the BFF uses as the key id.</param>
internal sealed record NhsnAppJwt(string CertificatePem, string PrivateKeyPem, string KeyId)
{
    /// <summary>The issuer the shell puts in its tokens and the BFF accepts. Nothing is fetched from it.</summary>
    public const string Issuer = "https://aspire-nhsn-app.example.org";

    /// <summary>
    /// Reads the pair made on an earlier run, or makes one. It is kept in a folder under the temp directory,
    /// so that a test profile saved in the browser, which holds the key, still works after a restart. It
    /// signs nothing outside this machine, and no key is committed.
    /// </summary>
    public static NhsnAppJwt LoadOrCreate()
    {
        var folder = Path.Combine(Path.GetTempPath(), "link-aspire", "nhsn-app-jwt");
        var certificatePath = Path.Combine(folder, "certificate.pem");
        var keyPath = Path.Combine(folder, "private-key.pem");

        if (File.Exists(certificatePath) && File.Exists(keyPath))
        {
            try
            {
                var certificatePem = File.ReadAllText(certificatePath);
                using var existing = X509Certificate2.CreateFromPem(certificatePem);
                if (existing.NotAfter > DateTime.Now.AddDays(1))
                    return new NhsnAppJwt(certificatePem, File.ReadAllText(keyPath), existing.Thumbprint);
            }
            catch (CryptographicException)
            {
                // Not a certificate any more: make a new pair below.
            }
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=NHSN App Aspire Public Certificate", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        var pair = new NhsnAppJwt(certificate.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), certificate.Thumbprint);
        Directory.CreateDirectory(folder);
        File.WriteAllText(certificatePath, pair.CertificatePem);
        File.WriteAllText(keyPath, pair.PrivateKeyPem);
        return pair;
    }
}
