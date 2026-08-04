# Validating a Vendor Secret ID Against Key Vault — Design

- **Ticket:** LEGLINK-566 (Validate Secret ID Against Key Vault)
- **Date:** 2026-08-04
- **Status:** Approved, pending implementation
- **Predecessor:** [2026-08-03 Vendor-Level Signing Key Association](2026-08-03-vendor-signing-key-secret-id-design.md)

## Story

As a system administrator, I want the system to validate that a secret ID I enter in the Vendor
Management UI actually exists and is active in Azure Key Vault, so that I receive immediate
feedback rather than discovering misconfigurations at runtime.

**Acceptance criteria**

1. On save (or on blur), the system calls Key Vault to verify the entered secret ID references a
   valid, active key.
2. An appropriate error message is shown if the secret ID does not resolve to a usable key.
3. Validation does not block save — warn but allow — to reduce runtime failures.

AC 2's original wording says "KID". As established in the predecessor design, the stored value is
the Key Vault **secret ID** (the name of the secret entry), not a JWKS `kid` header. This document
uses the accurate term throughout.

## Background

LEGLINK-620 added a JWT / Authentication section to the vendor form holding the Key Vault secret ID,
with no format validation: a format rule would have rejected ids the UI had no way to verify. This
story adds the verification that makes real feedback possible.

Without it, a mistyped or unprovisioned secret id is discovered only when Data Acquisition tries to
sign a JWT for an EHR request — far from the admin who typed it, and reported as an authentication
failure rather than a configuration error.

## Current state

Facts below were verified against the working tree on 2026-08-04.

**Admin.BFF is the UI's gateway.** `Web/Admin.UI` is configured with `baseApiUrl: /api`, and
`DotNet/Admin.BFF/Program.cs` maps a YARP reverse proxy plus its own minimal-API groups
(`/api/auth`, `/api/monitor`, `/api/aggregate`, `/api/integration`). Endpoint classes implement
`IApi` and register themselves — see `Presentation/Endpoints/BearerServiceEndpoints.cs`.

**The BFF already reaches Key Vault.** `Program.cs:167` calls `AddSecretManager`, which registers
either `AzureKeyVaultSecretManager` or `LocalSecretManager` from
`DotNet/Shared/Application/Services/SecretManager/`. `CreateLinkBearerToken` and `RefreshSigningKey`
already consume `ISecretManager` there. No new plumbing, no new YARP cluster, and no dependency on
where the `Vendor` model lives — which matters, because that model is mid-migration under
LEGLINK-743.

`DotNet/Admin.BFF/Infrastructure/SecretManagers/LinkAzureKeyVault.cs` also implements
`ISecretManager` but is **never registered**; it carries its own
`//TODO: Review this vs the AzureKeyVaultSecretManager in Shared`. This design does not touch it.

**`ISecretManager` returns secret values.** `GetSecretAsync` hands back the PEM itself. Any
validation surface must keep that server-side and report only status.

**The consumer's definition of usable.** `EpicAuth.CreateJwt`
(`DotNet/DataAcquisition.Domain/Application/Services/Auth/EpicAuth.cs:111-118`) reads the PEM with
BouncyCastle's `PemReader`, casts the result to `AsymmetricCipherKeyPair`, then casts
`.Private` to `RsaPrivateCrtKeyParameters`.

**Vendor editing is currently gated off.** `vendorEditEnabled` defaults to false because no vendor
update endpoint exists (`VendorController` has GET / GET-all / POST / DELETE only).

## Scope

**In scope**

- A validation endpoint on Admin.BFF
- `ISecretInspector` and `PemSigningKeyValidator` in `DotNet/Shared`
- Blur-time and save-time validation in the vendor form, with inline non-blocking warnings
- Unit tests on both sides and a mocked Playwright spec

**Out of scope**

- Per-row validation status in the vendor list (deferred "display key status" item)
- Changing `EpicAuth` to consume the shared validator — a worthwhile follow-up, but it touches the
  signing path and belongs in its own change
- Reconciling or removing the unregistered `LinkAzureKeyVault`
- The vendor update endpoint itself, still unowned (LEGLINK-743 covers list, add, delete)

## Architecture

```
Admin.UI  ──GET /api/secrets/{secretId}/validation──►  Admin.BFF
                                                          │
                                              ISecretInspector (Shared)
                                                          │
                                         Azure Key Vault  or  local encrypted file
                                                          │
                                              PemSigningKeyValidator (Shared)
```

Validation never touches the `Vendor` record, so nothing here depends on the Normalization →
Tenant migration.

### Endpoint

`SecretValidationEndpoints : IApi` in `DotNet/Admin.BFF/Presentation/Endpoints/`, following the
shape of `BearerServiceEndpoints`:

```
GET /api/secrets/{secretId}/validation
    .RequireAuthorization([PolicyNames.IsLinkAdmin])
```

`IsLinkAdmin` is the policy already guarding `VendorController`, so this exposes no reach an admin
lacks today.

**Response** — `200` whenever a check completes, including a negative result. A resolved "no such
secret" is a successful check, not a failed request, and one success shape keeps the client simple.

```json
{ "secretId": "epic-signing-pem", "status": "Valid", "message": "" }
```

| Code | When |
| --- | --- |
| `200` | The check ran; `status` carries the outcome |
| `400` | The name breaks Key Vault naming rules, or is empty |
| `401` / `403` | Policy |
| `500` | Unexpected fault, not a negative result |

`status` is a string enum: `Valid`, `NotFound`, `Disabled`, `Expired`, `NotYetValid`, `Unusable`,
`Unknown`. `Unknown` means Key Vault could not be reached or credentials failed — the UI must not
report "invalid" when it could not ask.

The handler composes `status` from the two Shared pieces:

| `SecretAvailability` | PEM classification | `status` |
| --- | --- | --- |
| `Available` | usable | `Valid` |
| `Available` | not usable | `Unusable` |
| `NotFound` / `Disabled` / `Expired` / `NotYetValid` | not run | same name |
| `Unavailable` | not run | `Unknown` |

`message` carries a server-side diagnostic string for logs and API consumers. The Admin UI renders
its own copy keyed off `status` (see below) rather than displaying `message`, so wording changes
do not require a backend deploy.

### `ISecretInspector` (Shared)

A separate interface rather than a widening of `ISecretManager`, so the three existing
implementations — including the dead `LinkAzureKeyVault` — stay untouched.

```csharp
public interface ISecretInspector
{
    Task<SecretInspection> InspectAsync(string secretName, CancellationToken cancellationToken);
}

public sealed record SecretInspection(SecretAvailability Availability, string? Value);

public enum SecretAvailability { Available, NotFound, Disabled, Expired, NotYetValid, Unavailable }
```

`Value` is populated only when `Availability` is `Available`, and only ever read inside the BFF —
the PEM classification needs it. It is never serialized into the response.

**Azure implementation.** Wraps `SecretClient`. A `RequestFailedException` with status 404 maps to
`NotFound`; 403 maps to `Disabled`; other failures map to `Unavailable`. On success, classify from
`secret.Properties`: `Enabled == false` → `Disabled`, `ExpiresOn` in the past → `Expired`,
`NotBefore` in the future → `NotYetValid`. Both paths are implemented because Key Vault treats
expiry as advisory on reads — an expired secret can still be returned rather than refused, so
properties must be checked and not merely the status code.

**Local implementation.** The local store is a name/value dictionary with no metadata, so it
returns `Available` or `NotFound` only. Local validation therefore cannot exercise `Disabled`,
`Expired`, or `NotYetValid`; this is a known limit of local development, not a defect.

Registration extends `AddSecretManager` so the inspector always matches the manager.

### `PemSigningKeyValidator` (Shared)

Uses `System.Security.Cryptography` only. BouncyCastle is not a direct dependency of any project
here — it appears only transitively in lock files — and adding it to `Shared` would push it into
every service that references `Shared`.

Classification, matching on the PEM label first:

| Input | Result |
| --- | --- |
| `BEGIN RSA PRIVATE KEY` (PKCS#1) that imports | `Valid` |
| `BEGIN PRIVATE KEY` (PKCS#8) | `Unusable` — see below |
| Public key, certificate, EC key, or unparseable | `Unusable` |

The PKCS#8 rule deserves its reasoning recorded. `EpicAuth` casts `PemReader.ReadObject()` to
`AsymmetricCipherKeyPair`; BouncyCastle returns a key pair for PKCS#1 but a bare
`AsymmetricKeyParameter` for PKCS#8, so the cast yields null and `.Private` throws a
`NullReferenceException` at signing time. A PKCS#8 RSA key that .NET imports without complaint is
therefore still unusable *for this consumer*. This inference comes from reading the code path, not
from a reproduction, so the implementation must include a test that feeds a PKCS#8 key through
`EpicAuth`'s parsing to confirm it before the rule ships. If the reproduction contradicts the
inference, relax the rule to accept PKCS#8 and note it here.

The validator returns a reason string alongside its verdict so the endpoint can compose a message
naming the required format.

## UI behavior

### Service

```ts
validateSecretId(secretId: string): Observable<ISecretValidationResult>
```

on `VendorService`, calling `GET ${baseApiPath}/secrets/${encodeURIComponent(secretId)}/validation`.

Its error path is quiet: a transport failure resolves to `{ status: 'Unknown' }` rather than
raising a toastr. A background check that cannot reach Key Vault must not throw an error popup at
an admin who is still typing. This differs deliberately from `handleSaveError`, which suppresses
only the toastr and rethrows.

### Form component

Status lives in a `secretIdStatus` field on the component, **not** in an Angular validator.
`submitConfiguration()` returns early when the form is invalid, so expressing this as form
validity would block save and contradict AC 3.

On blur of the secret id field:

1. Empty → clear status, make no call. An empty box means "clear the association", a legitimate
   save (the predecessor design sends explicit `null`).
2. Fails the Key Vault name rule (letters, digits, dashes; 1–127 characters) → show the format
   message locally, make no call.
3. Otherwise → `switchMap` to the service, showing "Checking…" while in flight.

Results are cached per value in a `Map`, so tabbing in and out repeatedly costs one call per
distinct value. On save, a value absent from that cache is checked once; the save proceeds
regardless of the outcome. `switchMap`, plus a guard discarding any response whose `secretId` no
longer matches the control, keeps a slow reply for an earlier value from overwriting a newer one.

### Messages

Rendered under the field in an `aria-live="polite"` region, styled as an amber warning rather than
the form's error red — the value is allowed either way. The Update button never changes state.

| Status | Message |
| --- | --- |
| `Valid` | Verified in Key Vault |
| `NotFound` | No secret by that name exists in Key Vault |
| `Disabled` | That secret is disabled in Key Vault |
| `Expired` | That secret's expiration date has passed |
| `NotYetValid` | That secret is not valid until a later date |
| `Unusable` | Resolves, but is not a usable RSA private key — Epic authentication needs a PKCS#1 `BEGIN RSA PRIVATE KEY` PEM |
| `Unknown` | Couldn't verify right now |

## Error handling

| Failure | Behavior |
| --- | --- |
| Key Vault unreachable or credentials fail | `Unknown`; neutral message; save unaffected |
| Validation request fails in transport | `Unknown`; no toastr |
| Secret resolves but is not a usable key | `Unusable`; warning; save still allowed |
| Save itself fails | Unchanged from the predecessor design — snackbar, dialog stays open |

No validation outcome blocks a save, and no validation outcome raises a toastr.

## Testing

**Backend unit** (`DotNet/ServiceTests`)

- `PemSigningKeyValidator`: PKCS#1 private key, PKCS#8 private key, public key, EC key, truncated
  PEM, empty string
- A reproduction test asserting how `EpicAuth`'s BouncyCastle path handles a PKCS#8 key, confirming
  the classification rule above
- Endpoint handler against a fake `ISecretInspector`: one case per `status`, `400` for a malformed
  name, `Unknown` when the inspector throws
- Azure inspector classification from `SecretProperties`, including expired-but-readable

**Frontend unit**

- One call per distinct value; none when empty; none when the name is malformed
- Save proceeds when the status is `Unusable`
- Save re-checks a value changed since the last check, and skips a cached one
- A stale response for a superseded value is discarded
- `Unknown` renders neutrally and raises no toastr

**Playwright, mocked** (`Web/Admin.UI` e2e)

With `vendorEditEnabled` on in the test config and the endpoint stubbed: enter a secret id that
reports `NotFound`, see the warning, save anyway, confirm the row updates.

No test touches a live Key Vault.

## Delivery

The BFF endpoint ships live and usable on its own. The UI half sits behind `vendorEditEnabled`
with the rest of the edit form, and becomes visible when the vendor update endpoint lands and the
flag is flipped.

## Open items

1. The vendor update endpoint remains unowned; this design does not resolve it.
2. Veradigm is expected to need a different key generation algorithm than Epic and Cerner. The
   validator's RSA/PKCS#1 rule reflects today's only consumer, `EpicAuth`. When a second algorithm
   arrives, classification will need to be vendor-aware rather than universal.
3. Whether an admin should be able to validate a secret id without an entitlement to read the
   secret's value. Today `IsLinkAdmin` covers both, and the endpoint returns no value, so the
   question is theoretical — but it becomes real if Link ever adds a narrower admin role.
