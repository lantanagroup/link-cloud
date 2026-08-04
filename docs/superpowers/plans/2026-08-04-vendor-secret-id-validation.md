# Vendor Secret ID Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tell a Link admin, while they are still editing, whether the Key Vault secret ID they typed for a vendor resolves to a usable RSA signing key — without ever blocking the save.

**Architecture:** A `GET /api/secrets/{secretId}/validation` endpoint on Admin.BFF, which already registers an `ISecretManager` and is independent of the Vendor model's migration to Tenant. Two small pieces go in `DotNet/Shared`: `ISecretInspector` (does the secret resolve, and is it active) and `PemSigningKeyValidator` (is the value a key `EpicAuth` can actually sign with). The Angular form validates on blur and once more at save, showing an inline amber warning that never touches form validity.

**Tech Stack:** .NET 8 minimal APIs, xUnit + Moq, Azure.Security.KeyVault.Secrets, Angular 17+ standalone components, Karma + Jasmine, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-04-vendor-secret-id-validation-design.md`

## Global Constraints

- **No new NuGet or npm packages.** The PEM check uses `System.Security.Cryptography`; BouncyCastle is not a direct dependency of `Shared` and must not become one.
- **Secret values never leave the server.** The endpoint returns status only. Never log a secret value.
- **Validation never blocks a save.** No Angular `Validators`, no disabled Update button, no confirm dialog.
- **Validation never raises a toastr.** Transport failures resolve to `Unknown`.
- **No test touches a live Key Vault.**
- The UI half stays behind the existing `vendorEditEnabled` flag; do not change its default.
- Do not modify `DotNet/Admin.BFF/Infrastructure/SecretManagers/LinkAzureKeyVault.cs` — it is unregistered dead code and out of scope.
- Status names used end to end, exactly: `Valid`, `NotFound`, `Disabled`, `Expired`, `NotYetValid`, `Unusable`, `Unknown`.

---

### Task 1: Confirm how EpicAuth handles a PKCS#8 key

The spec's rule "PKCS#8 is unusable" is an inference from reading
`DotNet/DataAcquisition.Domain/Application/Services/Auth/EpicAuth.cs:111-118`. It decides a rule in
Task 2, so it gets confirmed first, against the real signing path.

**Files:**
- Modify: `DotNet/ServiceTests/UnitTests/DataAcquisition/Services/Auth/EpicAuthTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: a confirmed or refuted premise for Task 2's `Classify` rules. No code artifact.

- [ ] **Step 1: Write the characterization test**

Add to `EpicAuthTests`. The existing fixture builds its PEM with `rsa.ExportRSAPrivateKeyPem()`
(PKCS#1); this one uses `ExportPkcs8PrivateKeyPem()`. Copy the surrounding test's arrange style —
read a neighboring test in the file for how `BuildSut` and `BuildAuthSettings` are called and what
handler the passing tests pass in.

```csharp
// Characterizes what EpicAuth does with a PKCS#8 PEM. BouncyCastle's PemReader returns a bare
// AsymmetricKeyParameter for PKCS#8 rather than an AsymmetricCipherKeyPair, so the cast in
// CreateJwt is expected to yield null and throw. PemSigningKeyValidator's classification depends
// on this behavior -- if this test ever changes, revisit it.
[Fact]
[Trait("Category", "UnitTests")]
public async Task CreateJwt_WithPkcs8Pem_Throws()
{
    using var rsa = RSA.Create(2048);
    var pkcs8Pem = rsa.ExportPkcs8PrivateKeyPem();

    _mockSecretManager
        .Setup(x => x.GetSecretAsync(KeySecretName, CancellationToken.None))
        .ReturnsAsync(pkcs8Pem);

    var sut = BuildSut(new Mock<HttpMessageHandler>().Object);

    await Assert.ThrowsAnyAsync<Exception>(
        () => sut.CreateJwt(FacilityId, BuildAuthSettings()));
}
```

Adjust the call to whatever `CreateJwt`'s real signature and access level are — if it is private,
exercise it through the public entry point the other tests use.

- [ ] **Step 2: Run it**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~EpicAuthTests"`

Two possible outcomes, and both are fine:

- **PASS** — the inference holds. Continue to Task 2 with the PKCS#8 rule as written.
- **FAIL** (no exception thrown) — the inference is wrong. Delete this test, and in Task 2 treat a
  PKCS#8 key that imports as `Valid` rather than `Unusable`, dropping the PKCS#8 branch and its
  test case. Then edit the spec's "PemSigningKeyValidator" section to record the correction.

Do not proceed until you know which outcome you got.

- [ ] **Step 3: Commit**

```bash
git add DotNet/ServiceTests/UnitTests/DataAcquisition/Services/Auth/EpicAuthTests.cs
git commit -m "LEGLINK-566: characterize EpicAuth's handling of a PKCS#8 PEM"
```

---

### Task 2: PemSigningKeyValidator

**Files:**
- Create: `DotNet/Shared/Application/Services/SecretManager/PemSigningKeyValidator.cs`
- Test: `DotNet/ServiceTests/UnitTests/Shared/PemSigningKeyValidatorTests.cs`

**Interfaces:**
- Consumes: Task 1's confirmed rule
- Produces:
  ```csharp
  namespace LantanaGroup.Link.Shared.Application.Services.SecretManager;
  public sealed record PemKeyClassification(bool IsUsable, string? Reason);
  public static class PemSigningKeyValidator
  {
      public static PemKeyClassification Classify(string? pem);
  }
  ```

- [ ] **Step 1: Write the failing tests**

Create `DotNet/ServiceTests/UnitTests/Shared/PemSigningKeyValidatorTests.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using System.Security.Cryptography;
using Xunit;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class PemSigningKeyValidatorTests
{
    [Fact]
    public void Classify_Pkcs1PrivateKey_IsUsable()
    {
        using var rsa = RSA.Create(2048);

        var result = PemSigningKeyValidator.Classify(rsa.ExportRSAPrivateKeyPem());

        Assert.True(result.IsUsable);
        Assert.Null(result.Reason);
    }

    // EpicAuth escapes newlines this way before parsing; a stored secret can carry them.
    [Fact]
    public void Classify_Pkcs1PrivateKeyWithEscapedNewlines_IsUsable()
    {
        using var rsa = RSA.Create(2048);
        var escaped = rsa.ExportRSAPrivateKeyPem().Replace("\r\n", "\\r\\n\\t").Replace("\n", "\\r\\n\\t");

        var result = PemSigningKeyValidator.Classify(escaped);

        Assert.True(result.IsUsable);
    }

    // See EpicAuthTests.CreateJwt_WithPkcs8Pem_Throws -- .NET imports this happily, the consumer does not.
    [Fact]
    public void Classify_Pkcs8PrivateKey_IsNotUsable()
    {
        using var rsa = RSA.Create(2048);

        var result = PemSigningKeyValidator.Classify(rsa.ExportPkcs8PrivateKeyPem());

        Assert.False(result.IsUsable);
        Assert.Contains("PKCS#1", result.Reason);
    }

    [Fact]
    public void Classify_PublicKey_IsNotUsable()
    {
        using var rsa = RSA.Create(2048);

        var result = PemSigningKeyValidator.Classify(rsa.ExportSubjectPublicKeyInfoPem());

        Assert.False(result.IsUsable);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Classify_EcPrivateKey_IsNotUsable()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var result = PemSigningKeyValidator.Classify(ec.ExportECPrivateKeyPem());

        Assert.False(result.IsUsable);
    }

    [Fact]
    public void Classify_TruncatedPem_IsNotUsable()
    {
        using var rsa = RSA.Create(2048);
        var truncated = rsa.ExportRSAPrivateKeyPem()[..120];

        var result = PemSigningKeyValidator.Classify(truncated);

        Assert.False(result.IsUsable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_Empty_IsNotUsable(string? pem)
    {
        var result = PemSigningKeyValidator.Classify(pem);

        Assert.False(result.IsUsable);
        Assert.NotNull(result.Reason);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~PemSigningKeyValidatorTests"`
Expected: build failure — `PemSigningKeyValidator` does not exist.

- [ ] **Step 3: Implement**

Create `DotNet/Shared/Application/Services/SecretManager/PemSigningKeyValidator.cs`:

```csharp
using System.Security.Cryptography;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    /// <summary>The outcome of inspecting a PEM. <paramref name="Reason"/> is null when usable.</summary>
    public sealed record PemKeyClassification(bool IsUsable, string? Reason);

    /// <summary>
    /// Decides whether a PEM is a signing key Link can actually use.
    ///
    /// "Usable" is defined by the consumer, EpicAuth, which reads the PEM with BouncyCastle's
    /// PemReader and casts to AsymmetricCipherKeyPair -- a shape only PKCS#1 produces. A PKCS#8
    /// key imports fine in .NET but makes that cast return null, so it is rejected here rather
    /// than failing when a JWT is signed. Uses System.Security.Cryptography deliberately:
    /// BouncyCastle is not a direct dependency of Shared and must not become one.
    /// </summary>
    public static class PemSigningKeyValidator
    {
        private const string Pkcs1Label = "BEGIN RSA PRIVATE KEY";
        private const string Pkcs8Label = "BEGIN PRIVATE KEY";

        private const string Pkcs8Reason =
            "The secret holds a PKCS#8 key. Epic authentication requires a PKCS#1 'BEGIN RSA PRIVATE KEY' PEM.";
        private const string NotAPrivateKeyReason =
            "The secret does not hold an RSA private key in PKCS#1 'BEGIN RSA PRIVATE KEY' form.";
        private const string EmptyReason = "The secret has no value.";

        public static PemKeyClassification Classify(string? pem)
        {
            if (string.IsNullOrWhiteSpace(pem))
            {
                return new PemKeyClassification(false, EmptyReason);
            }

            // EpicAuth applies the same normalization before parsing; a secret stored with escaped
            // newlines is usable there, so it must be usable here too.
            var normalized = pem.Replace("\\r\\n\\t", "\r\n\t");

            if (normalized.Contains(Pkcs8Label, StringComparison.Ordinal) &&
                !normalized.Contains(Pkcs1Label, StringComparison.Ordinal))
            {
                return new PemKeyClassification(false, Pkcs8Reason);
            }

            if (!normalized.Contains(Pkcs1Label, StringComparison.Ordinal))
            {
                return new PemKeyClassification(false, NotAPrivateKeyReason);
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(normalized);
                return new PemKeyClassification(true, null);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                return new PemKeyClassification(false, NotAPrivateKeyReason);
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~PemSigningKeyValidatorTests"`
Expected: all pass. If the escaped-newline test fails, check that the replacement in the test
produces the same escaping EpicAuth expects (`\r\n\t` as literal backslash characters).

- [ ] **Step 5: Commit**

```bash
git add DotNet/Shared/Application/Services/SecretManager/PemSigningKeyValidator.cs DotNet/ServiceTests/UnitTests/Shared/PemSigningKeyValidatorTests.cs
git commit -m "LEGLINK-566: classify whether a PEM is a usable signing key"
```

---

### Task 3: Secret availability classification

A pure function, split out so the Key Vault property rules are testable without an Azure client.

**Files:**
- Create: `DotNet/Shared/Application/Models/Secrets/SecretInspection.cs`
- Create: `DotNet/Shared/Application/Services/SecretManager/SecretAvailabilityClassifier.cs`
- Test: `DotNet/ServiceTests/UnitTests/Shared/SecretAvailabilityClassifierTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  ```csharp
  namespace LantanaGroup.Link.Shared.Application.Models.Secrets;
  public enum SecretAvailability { Available, NotFound, Disabled, Expired, NotYetValid, Unavailable }
  public sealed record SecretInspection(SecretAvailability Availability, string? Value);

  namespace LantanaGroup.Link.Shared.Application.Services.SecretManager;
  public static class SecretAvailabilityClassifier
  {
      public static SecretAvailability Classify(bool? enabled, DateTimeOffset? expiresOn, DateTimeOffset? notBefore, DateTimeOffset now);
  }
  ```

- [ ] **Step 1: Write the failing tests**

Create `DotNet/ServiceTests/UnitTests/Shared/SecretAvailabilityClassifierTests.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Models.Secrets;
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using Xunit;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class SecretAvailabilityClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Classify_EnabledWithNoDates_IsAvailable()
    {
        Assert.Equal(SecretAvailability.Available,
            SecretAvailabilityClassifier.Classify(enabled: true, expiresOn: null, notBefore: null, Now));
    }

    [Fact]
    public void Classify_NullEnabled_IsAvailable()
    {
        // Key Vault leaves Enabled unset on some secrets; absence is not disablement.
        Assert.Equal(SecretAvailability.Available,
            SecretAvailabilityClassifier.Classify(enabled: null, expiresOn: null, notBefore: null, Now));
    }

    [Fact]
    public void Classify_Disabled_IsDisabled()
    {
        Assert.Equal(SecretAvailability.Disabled,
            SecretAvailabilityClassifier.Classify(enabled: false, expiresOn: null, notBefore: null, Now));
    }

    // Key Vault treats expiry as advisory and still returns the value, so this must be caught here.
    [Fact]
    public void Classify_PastExpiry_IsExpired()
    {
        Assert.Equal(SecretAvailability.Expired,
            SecretAvailabilityClassifier.Classify(true, Now.AddDays(-1), null, Now));
    }

    [Fact]
    public void Classify_FutureNotBefore_IsNotYetValid()
    {
        Assert.Equal(SecretAvailability.NotYetValid,
            SecretAvailabilityClassifier.Classify(true, null, Now.AddDays(1), Now));
    }

    [Fact]
    public void Classify_DisabledAndExpired_PrefersDisabled()
    {
        Assert.Equal(SecretAvailability.Disabled,
            SecretAvailabilityClassifier.Classify(false, Now.AddDays(-1), null, Now));
    }

    [Fact]
    public void Classify_WithinDates_IsAvailable()
    {
        Assert.Equal(SecretAvailability.Available,
            SecretAvailabilityClassifier.Classify(true, Now.AddDays(1), Now.AddDays(-1), Now));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~SecretAvailabilityClassifierTests"`
Expected: build failure — the types do not exist.

- [ ] **Step 3: Implement**

Create `DotNet/Shared/Application/Models/Secrets/SecretInspection.cs`:

```csharp
namespace LantanaGroup.Link.Shared.Application.Models.Secrets
{
    public enum SecretAvailability
    {
        Available,
        NotFound,
        Disabled,
        Expired,
        NotYetValid,
        Unavailable
    }

    /// <summary>
    /// The result of inspecting a secret. <paramref name="Value"/> is populated only when
    /// <paramref name="Availability"/> is Available, and must never be serialized to a client.
    /// </summary>
    public sealed record SecretInspection(SecretAvailability Availability, string? Value);
}
```

Create `DotNet/Shared/Application/Services/SecretManager/SecretAvailabilityClassifier.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Models.Secrets;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    /// <summary>
    /// Turns a secret's Key Vault properties into an availability verdict. Separate from the
    /// client wrapper so the rules are testable without an Azure connection.
    /// </summary>
    public static class SecretAvailabilityClassifier
    {
        public static SecretAvailability Classify(
            bool? enabled,
            DateTimeOffset? expiresOn,
            DateTimeOffset? notBefore,
            DateTimeOffset now)
        {
            if (enabled == false)
            {
                return SecretAvailability.Disabled;
            }

            if (expiresOn is not null && expiresOn <= now)
            {
                return SecretAvailability.Expired;
            }

            if (notBefore is not null && notBefore > now)
            {
                return SecretAvailability.NotYetValid;
            }

            return SecretAvailability.Available;
        }
    }
}
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~SecretAvailabilityClassifierTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add DotNet/Shared/Application/Models/Secrets/SecretInspection.cs DotNet/Shared/Application/Services/SecretManager/SecretAvailabilityClassifier.cs DotNet/ServiceTests/UnitTests/Shared/SecretAvailabilityClassifierTests.cs
git commit -m "LEGLINK-566: classify Key Vault secret availability from its properties"
```

---

### Task 4: ISecretInspector and its two implementations

**Files:**
- Create: `DotNet/Shared/Application/Interfaces/Services/ISecretInspector.cs`
- Create: `DotNet/Shared/Application/Services/SecretManager/AzureKeyVaultSecretInspector.cs`
- Create: `DotNet/Shared/Application/Services/SecretManager/LocalSecretInspector.cs`
- Modify: `DotNet/Shared/Application/Extensions/Security/SecretManagerExtension.cs`
- Test: `DotNet/ServiceTests/UnitTests/Shared/LocalSecretInspectorTests.cs`

**Interfaces:**
- Consumes: `SecretInspection`, `SecretAvailability`, `SecretAvailabilityClassifier` (Task 3)
- Produces:
  ```csharp
  namespace LantanaGroup.Link.Shared.Application.Interfaces.Services;
  public interface ISecretInspector
  {
      Task<SecretInspection> InspectAsync(string secretName, CancellationToken cancellationToken);
  }
  ```
  Registered by `AddSecretManager` alongside the matching `ISecretManager`.

A separate interface, not extra methods on `ISecretManager`: widening that interface would force
changes on all three of its implementations, including the unregistered `LinkAzureKeyVault`.

- [ ] **Step 1: Write the failing test**

Create `DotNet/ServiceTests/UnitTests/Shared/LocalSecretInspectorTests.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Secrets;
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using Moq;
using Xunit;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class LocalSecretInspectorTests
{
    private readonly Mock<ISecretManager> _secretManager = new();

    private LocalSecretInspector BuildSut() => new(_secretManager.Object);

    [Fact]
    public async Task InspectAsync_KnownSecret_IsAvailableWithValue()
    {
        _secretManager
            .Setup(x => x.GetSecretAsync("epic-signing-pem", It.IsAny<CancellationToken>()))
            .ReturnsAsync("pem-content");

        var result = await BuildSut().InspectAsync("epic-signing-pem", CancellationToken.None);

        Assert.Equal(SecretAvailability.Available, result.Availability);
        Assert.Equal("pem-content", result.Value);
    }

    [Fact]
    public async Task InspectAsync_UnknownSecret_IsNotFound()
    {
        _secretManager
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await BuildSut().InspectAsync("nope", CancellationToken.None);

        Assert.Equal(SecretAvailability.NotFound, result.Availability);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task InspectAsync_WhenManagerThrows_IsUnavailable()
    {
        _secretManager
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await BuildSut().InspectAsync("epic-signing-pem", CancellationToken.None);

        Assert.Equal(SecretAvailability.Unavailable, result.Availability);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~LocalSecretInspectorTests"`
Expected: build failure — `LocalSecretInspector` does not exist.

- [ ] **Step 3: Implement the interface**

Create `DotNet/Shared/Application/Interfaces/Services/ISecretInspector.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Models.Secrets;

namespace LantanaGroup.Link.Shared.Application.Interfaces.Services
{
    /// <summary>
    /// Reports whether a secret resolves and is currently usable, without the caller needing to
    /// know how the underlying store signals absence. Separate from ISecretManager so that
    /// interface's implementations are unaffected.
    /// </summary>
    public interface ISecretInspector
    {
        Task<SecretInspection> InspectAsync(string secretName, CancellationToken cancellationToken);
    }
}
```

- [ ] **Step 4: Implement the local inspector**

Create `DotNet/Shared/Application/Services/SecretManager/LocalSecretInspector.cs`:

```csharp
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Secrets;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    /// <summary>
    /// Inspects the local development secret store, which holds names and values only. It has no
    /// enabled flag or validity dates, so Disabled, Expired and NotYetValid cannot occur locally.
    /// </summary>
    public class LocalSecretInspector : ISecretInspector
    {
        private readonly ISecretManager _secretManager;

        public LocalSecretInspector(ISecretManager secretManager)
        {
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        }

        public async Task<SecretInspection> InspectAsync(string secretName, CancellationToken cancellationToken)
        {
            try
            {
                var value = await _secretManager.GetSecretAsync(secretName, cancellationToken);

                return string.IsNullOrEmpty(value)
                    ? new SecretInspection(SecretAvailability.NotFound, null)
                    : new SecretInspection(SecretAvailability.Available, value);
            }
            catch (Exception)
            {
                return new SecretInspection(SecretAvailability.Unavailable, null);
            }
        }
    }
}
```

- [ ] **Step 5: Run to verify the tests pass**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~LocalSecretInspectorTests"`
Expected: all pass.

- [ ] **Step 6: Implement the Azure inspector**

No unit test — it is a thin wrapper over `SecretClient`, whose rules already have coverage in
`SecretAvailabilityClassifierTests`. Create
`DotNet/Shared/Application/Services/SecretManager/AzureKeyVaultSecretInspector.cs`:

```csharp
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Secrets;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Services.SecretManager
{
    /// <summary>
    /// Reports a secret's availability from Azure Key Vault. Key Vault refuses a disabled secret
    /// with 403 but returns an expired one, so both the failure status and the returned properties
    /// have to be consulted.
    /// </summary>
    public class AzureKeyVaultSecretInspector : ISecretInspector
    {
        private readonly ILogger<AzureKeyVaultSecretInspector> _logger;
        private readonly SecretClient _secretClient;

        public AzureKeyVaultSecretInspector(
            ILogger<AzureKeyVaultSecretInspector> logger,
            IOptions<SecretManagerSettings> secretManagerConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secretClient = new SecretClient(new Uri(secretManagerConfig.Value.ManagerUri), new DefaultAzureCredential());
        }

        public async Task<SecretInspection> InspectAsync(string secretName, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
                var secret = response.Value;

                var availability = SecretAvailabilityClassifier.Classify(
                    secret.Properties.Enabled,
                    secret.Properties.ExpiresOn,
                    secret.Properties.NotBefore,
                    DateTimeOffset.UtcNow);

                return availability == SecretAvailability.Available
                    ? new SecretInspection(availability, secret.Value)
                    : new SecretInspection(availability, null);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return new SecretInspection(SecretAvailability.NotFound, null);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                // Key Vault refuses reads of a disabled secret with 403.
                return new SecretInspection(SecretAvailability.Disabled, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not inspect secret {SecretName} in Key Vault", secretName.SanitizeAndRemove());
                return new SecretInspection(SecretAvailability.Unavailable, null);
            }
        }
    }
}
```

Check the `SanitizeAndRemove` import against `AzureKeyVaultSecretManager.cs` in the same folder,
which uses the same helper, and match its `using`. Confirm `SecretManagerSettings` is the options
type that file binds — reuse whatever it uses rather than introducing a second settings class.

- [ ] **Step 7: Register both inspectors**

Modify `DotNet/Shared/Application/Extensions/Security/SecretManagerExtension.cs` so each branch
registers its inspector next to its manager:

```csharp
            switch (secretManagerOptions.Manager)
            {
                case "Local":
                    services.AddSingleton<ISecretManager, LocalSecretManager>();
                    services.AddSingleton<ISecretInspector, LocalSecretInspector>();
                    break;
                case "AzureKeyVault":
                    services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
                    services.AddSingleton<ISecretInspector, AzureKeyVaultSecretInspector>();
                    break;

                default:
                    throw new ArgumentException("Invalid secret manager");
            }
```

- [ ] **Step 8: Build the affected services**

Run: `dotnet build DotNet/Shared/Shared.csproj && dotnet build DotNet/Admin.BFF/Admin.BFF.csproj`
Expected: both succeed. Every service calling `AddSecretManager` now also gets an `ISecretInspector`;
nothing else changes for them.

- [ ] **Step 9: Commit**

```bash
git add DotNet/Shared/Application/Interfaces/Services/ISecretInspector.cs DotNet/Shared/Application/Services/SecretManager/AzureKeyVaultSecretInspector.cs DotNet/Shared/Application/Services/SecretManager/LocalSecretInspector.cs DotNet/Shared/Application/Extensions/Security/SecretManagerExtension.cs DotNet/ServiceTests/UnitTests/Shared/LocalSecretInspectorTests.cs
git commit -m "LEGLINK-566: add ISecretInspector for Azure and local secret stores"
```

---

### Task 5: The Admin.BFF validation endpoint

**Files:**
- Create: `DotNet/Admin.BFF/Application/Models/Responses/SecretValidationResponse.cs`
- Create: `DotNet/Admin.BFF/Presentation/Endpoints/SecretValidationEndpoints.cs`
- Modify: `DotNet/Admin.BFF/Program.cs` (near line 230, with the other `IApi` registrations)
- Test: `DotNet/ServiceTests/UnitTests/AdminBFF/SecretValidationEndpointsTests.cs`

**Interfaces:**
- Consumes: `ISecretInspector`, `SecretInspection`, `SecretAvailability` (Task 4);
  `PemSigningKeyValidator.Classify` (Task 2)
- Produces: `GET /api/secrets/{secretId}/validation` returning
  `SecretValidationResponse(string SecretId, string Status, string Message)`

- [ ] **Step 1: Write the failing tests**

Create `DotNet/ServiceTests/UnitTests/AdminBFF/SecretValidationEndpointsTests.cs`. The handler is
tested directly rather than over HTTP, matching how other endpoint logic is covered in this project:

```csharp
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using Xunit;

namespace UnitTests.AdminBFF;

[Trait("Category", "UnitTests")]
public class SecretValidationEndpointsTests
{
    private readonly Mock<ILogger<SecretValidationEndpoints>> _logger = new();
    private readonly Mock<ISecretInspector> _inspector = new();
    private readonly string _usablePem;

    public SecretValidationEndpointsTests()
    {
        using var rsa = RSA.Create(2048);
        _usablePem = rsa.ExportRSAPrivateKeyPem();
    }

    private SecretValidationEndpoints BuildSut() => new(_logger.Object, _inspector.Object);

    private void InspectorReturns(SecretAvailability availability, string? value = null) =>
        _inspector
            .Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretInspection(availability, value));

    private static SecretValidationResponse Body(IResult result) =>
        Assert.IsType<Ok<SecretValidationResponse>>(result).Value!;

    [Fact]
    public async Task Validate_UsableKey_IsValid()
    {
        InspectorReturns(SecretAvailability.Available, _usablePem);

        var result = await BuildSut().Validate("epic-signing-pem", CancellationToken.None);

        Assert.Equal("Valid", Body(result).Status);
    }

    [Fact]
    public async Task Validate_ResolvesButIsNotAKey_IsUnusable()
    {
        InspectorReturns(SecretAvailability.Available, "not a pem");

        var result = await BuildSut().Validate("epic-signing-pem", CancellationToken.None);

        var body = Body(result);
        Assert.Equal("Unusable", body.Status);
        Assert.NotEmpty(body.Message);
    }

    [Theory]
    [InlineData(SecretAvailability.NotFound, "NotFound")]
    [InlineData(SecretAvailability.Disabled, "Disabled")]
    [InlineData(SecretAvailability.Expired, "Expired")]
    [InlineData(SecretAvailability.NotYetValid, "NotYetValid")]
    [InlineData(SecretAvailability.Unavailable, "Unknown")]
    public async Task Validate_MapsAvailabilityToStatus(SecretAvailability availability, string expected)
    {
        InspectorReturns(availability);

        var result = await BuildSut().Validate("epic-signing-pem", CancellationToken.None);

        Assert.Equal(expected, Body(result).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has spaces")]
    [InlineData("has/slash")]
    public async Task Validate_MalformedName_IsBadRequest(string secretId)
    {
        var result = await BuildSut().Validate(secretId, CancellationToken.None);

        Assert.IsType<BadRequest<string>>(result);
        _inspector.Verify(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Validate_WhenInspectorThrows_IsUnknown()
    {
        _inspector
            .Setup(x => x.InspectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("vault down"));

        var result = await BuildSut().Validate("epic-signing-pem", CancellationToken.None);

        Assert.Equal("Unknown", Body(result).Status);
    }

    [Fact]
    public async Task Validate_NeverReturnsTheSecretValue()
    {
        InspectorReturns(SecretAvailability.Available, _usablePem);

        var result = await BuildSut().Validate("epic-signing-pem", CancellationToken.None);

        var body = Body(result);
        Assert.DoesNotContain("PRIVATE KEY", body.Message);
        Assert.DoesNotContain("PRIVATE KEY", body.SecretId);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~SecretValidationEndpointsTests"`
Expected: build failure — `SecretValidationEndpoints` does not exist.

- [ ] **Step 3: Write the response model**

Create `DotNet/Admin.BFF/Application/Models/Responses/SecretValidationResponse.cs`:

```csharp
namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    /// <summary>
    /// The outcome of checking a Key Vault secret id. Carries status only -- the secret's value
    /// is never serialized.
    /// </summary>
    public class SecretValidationResponse
    {
        public string SecretId { get; set; } = string.Empty;

        /// <summary>Valid, NotFound, Disabled, Expired, NotYetValid, Unusable or Unknown.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Diagnostic detail for logs and API consumers; the Admin UI renders its own copy.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4: Write the endpoint**

Create `DotNet/Admin.BFF/Presentation/Endpoints/SecretValidationEndpoints.cs`:

```csharp
using System.Text.RegularExpressions;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Secrets;
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using Link.Authorization.Policies;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    /// <summary>
    /// Lets an admin check a Key Vault secret id before saving it against a vendor, so a bad id is
    /// caught while it is being typed rather than when Data Acquisition next signs a JWT.
    /// Returns status only; the secret's value never leaves this process.
    /// </summary>
    public class SecretValidationEndpoints(
        ILogger<SecretValidationEndpoints> logger,
        ISecretInspector secretInspector)
        : IApi
    {
        private readonly ILogger<SecretValidationEndpoints> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ISecretInspector _secretInspector = secretInspector ?? throw new ArgumentNullException(nameof(secretInspector));

        // Azure Key Vault secret names: letters, digits and dashes, 1-127 characters.
        private static readonly Regex SecretNamePattern = new("^[0-9a-zA-Z-]{1,127}$", RegexOptions.Compiled);

        public void RegisterEndpoints(WebApplication app)
        {
            var secretEndpoints = app.MapGroup("/api/secrets")
                .RequireAuthorization([PolicyNames.IsLinkAdmin])
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Secrets" } }
                });

            secretEndpoints.MapGet("/{secretId}/validation", (Delegate)Validate)
                .Produces<SecretValidationResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Validate a Key Vault secret id.",
                    Description = "Reports whether the secret resolves, is active, and holds a usable signing key. Never returns the secret's value."
                });

            _logger.LogApiRegistration(nameof(SecretValidationEndpoints));
        }

        public async Task<IResult> Validate(string secretId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(secretId) || !SecretNamePattern.IsMatch(secretId))
            {
                return Results.BadRequest("A secret id may contain only letters, numbers and dashes.");
            }

            SecretInspection inspection;
            try
            {
                inspection = await _secretInspector.InspectAsync(secretId, cancellationToken);
            }
            catch (Exception ex)
            {
                // An unreachable vault is not an invalid secret id; say so rather than accusing the admin.
                _logger.LogWarning(ex, "Secret validation could not complete for {SecretId}", secretId);
                inspection = new SecretInspection(SecretAvailability.Unavailable, null);
            }

            return Results.Ok(Describe(secretId, inspection));
        }

        private static SecretValidationResponse Describe(string secretId, SecretInspection inspection)
        {
            if (inspection.Availability != SecretAvailability.Available)
            {
                return new SecretValidationResponse
                {
                    SecretId = secretId,
                    Status = inspection.Availability == SecretAvailability.Unavailable
                        ? "Unknown"
                        : inspection.Availability.ToString(),
                    Message = inspection.Availability switch
                    {
                        SecretAvailability.NotFound => "No secret by that name exists in the vault.",
                        SecretAvailability.Disabled => "That secret is disabled in the vault.",
                        SecretAvailability.Expired => "That secret's expiration date has passed.",
                        SecretAvailability.NotYetValid => "That secret is not valid until a later date.",
                        _ => "The vault could not be reached."
                    }
                };
            }

            var classification = PemSigningKeyValidator.Classify(inspection.Value);

            return new SecretValidationResponse
            {
                SecretId = secretId,
                Status = classification.IsUsable ? "Valid" : "Unusable",
                Message = classification.Reason ?? string.Empty
            };
        }
    }
}
```

- [ ] **Step 5: Register the endpoint class**

Modify `DotNet/Admin.BFF/Program.cs`, beside the other `IApi` registrations near line 230:

```csharp
        builder.Services.AddTransient<IApi, SecretValidationEndpoints>();
```

Match the surrounding conditional structure — read lines 225-240 first, since some registrations
there sit inside `if` blocks. This one is unconditional.

- [ ] **Step 6: Run to verify the tests pass**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "FullyQualifiedName~SecretValidationEndpointsTests"`
Expected: all pass. If `Ok<SecretValidationResponse>` does not match, check whether
`Results.Ok(...)` returns `Ok<T>` in this .NET version and adjust the assertion helper only.

- [ ] **Step 7: Build the BFF**

Run: `dotnet build DotNet/Admin.BFF/Admin.BFF.csproj`
Expected: success.

- [ ] **Step 8: Commit**

```bash
git add DotNet/Admin.BFF/Application/Models/Responses/SecretValidationResponse.cs DotNet/Admin.BFF/Presentation/Endpoints/SecretValidationEndpoints.cs DotNet/Admin.BFF/Program.cs DotNet/ServiceTests/UnitTests/AdminBFF/SecretValidationEndpointsTests.cs
git commit -m "LEGLINK-566: add a Key Vault secret id validation endpoint to Admin.BFF"
```

---

### Task 6: Angular service call

**Files:**
- Create: `Web/Admin.UI/src/app/interfaces/vendor/secret-validation-result.interface.ts`
- Modify: `Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.ts`
- Test: `Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.spec.ts`

**Interfaces:**
- Consumes: the endpoint from Task 5
- Produces:
  ```ts
  export type SecretValidationStatus =
    'Valid' | 'NotFound' | 'Disabled' | 'Expired' | 'NotYetValid' | 'Unusable' | 'Unknown';

  export interface ISecretValidationResult { secretId: string; status: SecretValidationStatus; message: string; }

  // on VendorService
  validateSecretId(secretId: string): Observable<ISecretValidationResult>
  ```

- [ ] **Step 1: Write the failing tests**

Append to `Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.spec.ts`, inside the
existing `describe`:

```ts
  it('validates a secret id against the BFF endpoint', () => {
    let result: ISecretValidationResult | undefined;
    service.validateSecretId('epic-signing-pem').subscribe(r => (result = r));

    const req = http.expectOne(`${BASE}/secrets/epic-signing-pem/validation`);
    expect(req.request.method).toBe('GET');
    req.flush({ secretId: 'epic-signing-pem', status: 'Valid', message: '' });

    expect(result?.status).toBe('Valid');
  });

  it('escapes the secret id in the validation route', () => {
    service.validateSecretId('a b').subscribe();

    const req = http.expectOne(`${BASE}/secrets/a%20b/validation`);
    req.flush({ secretId: 'a b', status: 'NotFound', message: '' });
  });

  // A background check that cannot reach the vault must not report the id invalid, and must not
  // throw an error popup at an admin who is still typing.
  it('reports Unknown and raises no toastr when validation fails', () => {
    let result: ISecretValidationResult | undefined;
    service.validateSecretId('epic-signing-pem').subscribe(r => (result = r));

    http.expectOne(`${BASE}/secrets/epic-signing-pem/validation`)
      .flush({ detail: 'nope' }, { status: 503, statusText: 'Service Unavailable' });

    expect(result?.status).toBe('Unknown');
    expect(errorHandler.handleError).not.toHaveBeenCalled();
  });
```

Add the import at the top of the spec:

```ts
import { ISecretValidationResult } from '../../../interfaces/vendor/secret-validation-result.interface';
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd Web/Admin.UI && npx ng test --watch=false --browsers=ChromeHeadless --include=**/vendor.service.spec.ts`
Expected: compilation failure — `validateSecretId` does not exist.

- [ ] **Step 3: Add the interface**

Create `Web/Admin.UI/src/app/interfaces/vendor/secret-validation-result.interface.ts`:

```ts
export type SecretValidationStatus =
  | 'Valid'
  | 'NotFound'
  | 'Disabled'
  | 'Expired'
  | 'NotYetValid'
  | 'Unusable'
  | 'Unknown';

export interface ISecretValidationResult {
  secretId: string;
  status: SecretValidationStatus;
  message: string;
}
```

- [ ] **Step 4: Add the service method**

Modify `Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.ts`. Add the imports
(`of` from `rxjs`, and the interface), then:

```ts
  /**
   * Checks a Key Vault secret id without saving anything. Failures resolve to `Unknown` rather
   * than erroring: this runs in the background while an admin types, so a vault that cannot be
   * reached must not be reported as a bad id, and must not raise a toastr. The endpoint returns
   * status only -- never the secret's value.
   */
  validateSecretId(secretId: string): Observable<ISecretValidationResult> {
    return this.http.get<ISecretValidationResult>(
      `${this.baseApiPath}/secrets/${encodeURIComponent(secretId)}/validation`)
      .pipe(
        catchError(() => of({ secretId, status: 'Unknown' as const, message: '' }))
      )
  }
```

- [ ] **Step 5: Run to verify they pass**

Run: `cd Web/Admin.UI && npx ng test --watch=false --browsers=ChromeHeadless --include=**/vendor.service.spec.ts`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add Web/Admin.UI/src/app/interfaces/vendor/secret-validation-result.interface.ts Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.ts Web/Admin.UI/src/app/services/gateway/vendor/vendor.service.spec.ts
git commit -m "LEGLINK-566: call the secret id validation endpoint from the vendor service"
```

---

### Task 7: Validate on blur and on save in the vendor form

**Files:**
- Modify: `Web/Admin.UI/src/app/components/vendor/vendor-config-form/vendor-config-form.component.ts`
- Modify: `Web/Admin.UI/src/app/components/vendor/vendor-config-form/vendor-config-form.component.html`
- Modify: `Web/Admin.UI/src/app/components/vendor/vendor-config-form/vendor-config-form.component.scss`
- Test: `Web/Admin.UI/src/app/components/vendor/vendor-config-form/vendor-config-form.component.spec.ts`

**Interfaces:**
- Consumes: `VendorService.validateSecretId`, `ISecretValidationResult`, `SecretValidationStatus` (Task 6)
- Produces: component members `secretIdStatus`, `secretIdMessage`, `onSecretIdBlur()`

- [ ] **Step 1: Write the failing tests**

Append to `vendor-config-form.component.spec.ts` inside the existing `describe`. Add
`'validateSecretId'` to the `createSpyObj` list in `beforeEach` and default it:

```ts
    vendorService = jasmine.createSpyObj<VendorService>(
      'VendorService', ['createVendor', 'updateVendor', 'validateSecretId']);
    vendorService.validateSecretId.and.returnValue(
      of({ secretId: '', status: 'Valid' as const, message: '' }));
```

Then the tests:

```ts
  function validationReturns(status: SecretValidationStatus): void {
    vendorService.validateSecretId.and.returnValue(of({ secretId: 'x', status, message: '' }));
  }

  it('validates the secret id on blur and reports the outcome', () => {
    validationReturns('NotFound');
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('missing-pem');
    component.onSecretIdBlur();

    expect(vendorService.validateSecretId).toHaveBeenCalledWith('missing-pem');
    expect(component.secretIdStatus).toBe('NotFound');
    expect(component.secretIdMessage).toContain('No secret');
  });

  it('does not validate an empty box, which means clearing the association', () => {
    initWith(epic, FormMode.Edit);

    component.secretId.setValue('   ');
    component.onSecretIdBlur();

    expect(vendorService.validateSecretId).not.toHaveBeenCalled();
    expect(component.secretIdStatus).toBeNull();
  });

  it('rejects a malformed name locally without calling the service', () => {
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('has spaces');
    component.onSecretIdBlur();

    expect(vendorService.validateSecretId).not.toHaveBeenCalled();
    expect(component.secretIdStatus).toBe('InvalidName');
    expect(component.secretIdMessage).toContain('letters, numbers and dashes');
  });

  // A slow reply for a value the admin has already replaced must not overwrite the newer verdict.
  it('discards a response for a value that is no longer in the box', () => {
    const slow = new Subject<ISecretValidationResult>();
    vendorService.validateSecretId.and.returnValue(slow.asObservable());
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('first-pem');
    component.onSecretIdBlur();

    component.secretId.setValue('second-pem');
    slow.next({ secretId: 'first-pem', status: 'NotFound', message: '' });

    expect(component.secretIdStatus).toBe('Checking');
  });

  it('checks each distinct value once', () => {
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('cerner-signing-pem');
    component.onSecretIdBlur();
    component.onSecretIdBlur();

    expect(vendorService.validateSecretId).toHaveBeenCalledTimes(1);

    component.secretId.setValue('other-pem');
    component.onSecretIdBlur();

    expect(vendorService.validateSecretId).toHaveBeenCalledTimes(2);
  });

  // AC 3: warn but allow. A failed check must never stop the save.
  it('saves anyway when the secret id does not validate', () => {
    vendorService.updateVendor.and.returnValue(of({ success: true, message: '' }));
    validationReturns('Unusable');
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('public-key-pem');
    component.onSecretIdBlur();
    component.submitConfiguration();

    expect(vendorService.updateVendor).toHaveBeenCalled();
    expect(component.secretIdStatus).toBe('Unusable');
  });

  it('checks a value changed since the last check before saving', () => {
    vendorService.updateVendor.and.returnValue(of({ success: true, message: '' }));
    validationReturns('Valid');
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('never-blurred-pem');
    component.submitConfiguration();

    expect(vendorService.validateSecretId).toHaveBeenCalledWith('never-blurred-pem');
    expect(vendorService.updateVendor).toHaveBeenCalled();
  });

  it('reports Unknown neutrally rather than calling the id invalid', () => {
    validationReturns('Unknown');
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('epic-signing-pem');
    component.onSecretIdBlur();

    expect(component.secretIdStatus).toBe('Unknown');
    expect(component.secretIdMessage).toContain("Couldn't verify");
  });

  // Validation must never make the form invalid -- submitConfiguration returns early when it is.
  it('leaves form validity untouched', () => {
    validationReturns('NotFound');
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('missing-pem');
    component.onSecretIdBlur();

    expect(component.vendorForm.valid).toBeTrue();
    expect(component.secretId.errors).toBeNull();
  });
```

Add the imports:

```ts
import { Subject } from 'rxjs';
import { ISecretValidationResult, SecretValidationStatus } from '../../../interfaces/vendor/secret-validation-result.interface';
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd Web/Admin.UI && npx ng test --watch=false --browsers=ChromeHeadless --include=**/vendor-config-form.component.spec.ts`
Expected: compilation failure — `onSecretIdBlur` does not exist.

- [ ] **Step 3: Implement the component logic**

Modify `vendor-config-form.component.ts`. Add imports for `ISecretValidationResult`,
`SecretValidationStatus`, then add these members and rework `submitConfiguration`'s Edit branch:

```ts
  /**
   * Null until a check has run for the current value. 'Checking' while one is in flight, and
   * 'InvalidName' for a value rejected locally — that one is client-only, since the server never
   * returns it (a malformed name gets a 400, not a status).
   */
  secretIdStatus: SecretValidationStatus | 'Checking' | 'InvalidName' | null = null;

  /** Results by value, so tabbing in and out repeatedly costs one call per distinct value. */
  private readonly checkedSecretIds = new Map<string, SecretValidationStatus>();

  // Azure Key Vault secret names: letters, digits and dashes, 1-127 characters. Checked here so an
  // obviously malformed name costs no round trip.
  private static readonly SECRET_NAME_PATTERN = /^[0-9a-zA-Z-]{1,127}$/;

  private static readonly SECRET_ID_MESSAGES: Record<string, string> = {
    Checking: 'Checking…',
    InvalidName: 'Secret ids may contain only letters, numbers and dashes',
    Valid: 'Verified in Key Vault',
    NotFound: 'No secret by that name exists in Key Vault',
    Disabled: 'That secret is disabled in Key Vault',
    Expired: "That secret's expiration date has passed",
    NotYetValid: 'That secret is not valid until a later date',
    Unusable: "Resolves, but is not a usable RSA private key — Epic authentication needs a PKCS#1 'BEGIN RSA PRIVATE KEY' PEM",
    Unknown: "Couldn't verify right now"
  };

  get secretIdMessage(): string {
    return this.secretIdStatus ? VendorConfigFormComponent.SECRET_ID_MESSAGES[this.secretIdStatus] : '';
  }

  /** True for anything the admin should see amber text about. */
  get secretIdWarning(): boolean {
    return this.secretIdStatus !== null && this.secretIdStatus !== 'Valid' && this.secretIdStatus !== 'Checking';
  }

  onSecretIdBlur(): void {
    const value = this.secretId.value?.trim() ?? '';

    // An empty box means "clear the association", which is a legitimate save, not a bad id.
    if (!value) {
      this.secretIdStatus = null;
      return;
    }

    if (!VendorConfigFormComponent.SECRET_NAME_PATTERN.test(value)) {
      this.secretIdStatus = 'InvalidName';
      return;
    }

    const cached = this.checkedSecretIds.get(value);
    if (cached) {
      this.secretIdStatus = cached;
      return;
    }

    this.checkSecretId(value).subscribe();
  }

  /**
   * Runs a check and records it. Discards a reply whose value is no longer in the box, so a slow
   * response for an earlier value cannot overwrite a newer verdict.
   */
  private checkSecretId(value: string): Observable<ISecretValidationResult> {
    this.secretIdStatus = 'Checking';

    return this.vendorService.validateSecretId(value).pipe(
      tap(result => {
        this.checkedSecretIds.set(value, result.status);
        if ((this.secretId.value?.trim() ?? '') === value) {
          this.secretIdStatus = result.status;
        }
      })
    );
  }
```

Then rework the Edit branch of `submitConfiguration()` so the save is reached in every case:

```ts
    const updated: IVendorConfigModel = {
      ...this.item,
      name: this.name.value,
      secretId: this.secretId.value?.trim() || null
    };

    // Warn but allow: an unchecked value is checked once here, and the save proceeds whatever the
    // outcome. The warning stays on screen; it never blocks.
    const value = updated.secretId;
    if (value && !this.checkedSecretIds.has(value) &&
        VendorConfigFormComponent.SECRET_NAME_PATTERN.test(value)) {
      this.checkSecretId(value).subscribe(() => this.saveVendor(updated));
      return;
    }

    this.saveVendor(updated);
  }

  private saveVendor(updated: IVendorConfigModel): void {
    this.vendorService.updateVendor(updated).subscribe({
      next: () => {
        this.submittedConfiguration.emit({success: true, message: ""});
      },
      error: (err) => {
        this.submittedConfiguration.emit({success: false, message: this.failureMessage(err)});
      }
    });
  }
```

Keep the existing comment above `updated` explaining the explicit `null`. Add `Observable` and
`tap` to the rxjs imports.

- [ ] **Step 4: Add the template and styles**

In `vendor-config-form.component.html`, inside the expansion panel, directly after the closing
`</mat-form-field>` of the secret id field:

```html
          @if (secretIdStatus) {
            <div class="secret-id-status"
                 [class.warning]="secretIdWarning"
                 aria-live="polite"
                 data-testid="vendor-secret-id-status">
              {{ secretIdMessage }}
            </div>
          }
```

And add the `(blur)` handler to the existing input:

```html
            <input matInput formControlName="secretId" [readonly]="viewOnly"
                   (blur)="onSecretIdBlur()"
                   data-testid="vendor-secret-id-input">
```

In `vendor-config-form.component.scss`:

```scss
.secret-id-status {
  font-size: 12px;
  margin: -8px 0 12px 16px;

  // Amber, not the form's error red: the value is allowed either way.
  &.warning {
    color: #b26500;
  }
}
```

- [ ] **Step 5: Run to verify they pass**

Run: `cd Web/Admin.UI && npx ng test --watch=false --browsers=ChromeHeadless --include=**/vendor/**/*.spec.ts`
Expected: all pass, including the pre-existing form and dashboard specs.

- [ ] **Step 6: Commit**

```bash
git add Web/Admin.UI/src/app/components/vendor/vendor-config-form/
git commit -m "LEGLINK-566: warn when a vendor's secret id does not validate, without blocking save"
```

---

### Task 8: Mocked Playwright coverage

**Precondition.** The Playwright suite lives on `feature/admin-ui-playwright-e2e` (PR #1773) and is
**not** in `dev` — `Web/Admin.UI` has no `playwright.config.ts` on this branch. Check first:

```bash
test -f Web/Admin.UI/playwright.config.ts && echo present || echo absent
```

If absent, skip this task, note in the PR description that e2e coverage follows PR #1773, and stop
here. Do not port the harness.

**Files (only if present):**
- Create: `Web/Admin.UI/e2e/tests/vendor-secret-validation.spec.ts` — match the directory and
  naming of the existing mocked specs
- Modify: whichever fixture file provides the mocked `app.config.json`, to set
  `vendorEditEnabled: true`

**Interfaces:**
- Consumes: `data-testid="vendor-secret-id-input"`, `data-testid="vendor-secret-id-status"`,
  `aria-label="Edit Vendor"`, and the `/api/secrets/*/validation` route from Task 5

- [ ] **Step 1: Read the existing mocked specs**

Read two of them and the ApiMock fixture before writing anything. Follow their route-stubbing
helper rather than calling `page.route` directly, and follow their console-error tripwire
convention.

- [ ] **Step 2: Write the spec**

```ts
test('warns about an unresolvable secret id but still saves', async ({ page }) => {
  // Stub: the vendor list, the validation endpoint returning NotFound, and the update endpoint.
  // Use the suite's existing mock helper for all three.

  await page.getByLabel('Edit Vendor').first().click();
  await page.getByTestId('vendor-secret-id-input').fill('missing-pem');
  await page.getByTestId('vendor-secret-id-input').blur();

  await expect(page.getByTestId('vendor-secret-id-status'))
    .toContainText('No secret by that name exists');

  await page.getByRole('button', { name: /update vendor configuration/i }).click();

  // Warn but allow: the dialog closes and the row shows the saved value.
  await expect(page.getByRole('dialog')).toBeHidden();
});
```

Fill in the mock calls from the fixture's actual API once you have read it.

- [ ] **Step 3: Run it**

Run: `cd Web/Admin.UI && npx playwright test vendor-secret-validation --reporter=list`
Expected: pass.

- [ ] **Step 4: Commit**

```bash
git add Web/Admin.UI/e2e
git commit -m "LEGLINK-566: mocked e2e for vendor secret id validation"
```

---

## Final verification

- [ ] **Full .NET unit suite**

Run: `dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter "Category=UnitTests"`
Expected: no new failures. Record the pass/fail count before starting Task 1 so the comparison is
against a known baseline.

- [ ] **Full Admin.UI unit suite**

Run: `cd Web/Admin.UI && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: the vendor specs all pass. This suite has a **known pre-existing failure population** —
roughly 56 failures from `NG0201: No provider found for InjectionToken ToastConfig` in default
`should create` specs across unrelated components. Compare against the baseline; do not try to fix
them here.

- [ ] **Confirm the flag still ships off**

Run: `grep vendorEditEnabled Web/Admin.UI/src/assets/app.config.json`
Expected: `"vendorEditEnabled": false`. The UI half of this work stays gated until a vendor update
endpoint exists.
