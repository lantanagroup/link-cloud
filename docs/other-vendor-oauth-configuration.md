# "Other" vendor OAuth configuration

Design notes for `FhirAuthenticationConfigurationService` and the endpoint in front of it,
`GET` / `PUT /api/data-acquisition/facilities/{facilityId}/fhir-authentication-configuration`
(LEGLINK-1005).

The endpoint stores generic OAuth client-credentials settings for a facility whose EHR vendor is
**Other**. The caller supplies the client secret as a plaintext value; the service writes it to the
secret manager and records only the secret *name* on the facility's existing
`fhirQueryConfiguration.Authentication` column, which is what `OAuth.cs` already resolves at
acquisition time.

## Where each piece of the configuration lives

| On the configuration row | In the secret manager |
| --- | --- |
| `AuthType = "OAuth"`, `TokenUrl`, `Scope` | the client id **value** |
| `ClientId` / `ClientSecret` — the secret **names** | the client secret **value** |

That split is not a choice: `OAuth.SetAuthentication` reads `TokenUrl` and `Scope` straight off the
row and passes `ClientId` and `ClientSecret` to `ISecretManager.GetSecretAsync` as names.

The client id value goes to the secret manager alongside the secret because the runtime resolves
both through `ISecretManager`. Storing the id in plaintext on the row would mean teaching `OAuth.cs`
to tell a value from a name — a change to a path every OAuth facility already uses.

## Secret naming

`fhir-oauth-{slug}-{hash}-client-id` and `-client-secret`, where `slug` is the facility id
lower-cased with anything outside `[0-9a-z-]` replaced by `-`, truncated so the whole name fits
Key Vault's `^[0-9a-zA-Z-]{1,127}$`, and `hash` is the first eight hex characters of SHA-256 over the
raw facility id.

The hash is what keeps facility ids that collapse to the same slug apart — `a.b` and `a_b` would
otherwise share a secret. The names are deterministic so an operator can find a facility's
credentials in the vault without consulting the database.

An existing secret name is reused rather than replaced when the caller omits `clientSecret`, because
an operator may have provisioned that secret by hand under a name of their own through the older
`api/data/{facilityId}/{queryConfigurationType}/authentication` endpoint.

## Write ordering, and the window it leaves

The service writes the secrets, then updates the row:

1. Confirm the facility has a `fhirQueryConfiguration` row. Without this the manager's own check
   would not fire until after the secrets had been written, leaving credentials in the vault for a
   facility with nothing to attach them to — and a missing row would surface as the `ClientSecret`
   `400` rather than a `404`.
2. Write the client id, and the client secret when one was supplied.
3. Update the row to reference those names.
4. Evict the facility's cached access token.

**Secrets before the row** means the row can never point at a name that was never written, which
would leave acquisition unable to resolve the credentials at all.

**The cost shows on a repeat write.** Because the names are deterministic, a second `PUT` writes over
the values the row already references. If step 3 then fails, the facility keeps its old token URL and
scope against credentials that have already changed. A retry converges, and the case only bites when
a single request changes both the token URL and the credentials *and* the database write fails.

The alternative — a fresh name per write, switched atomically by the row update — was considered and
rejected. It trades a narrow, self-healing window for secrets that accumulate in the vault with
nothing referencing them, cleanup code that can itself fail, and the loss of the deterministic name
above. Raised by CodeRabbit on PR #1948 and answered there.

## Facility ids are rejected, not repaired

`HtmlInputSanitizer.SanitizeAndRemove` strips characters rather than failing, so `fac!ility@1`
becomes `facility1`. For an endpoint that reads and overwrites credentials that is an identity
change: a caller could be served, or could overwrite, a different facility than the one they named.
The controller therefore rejects any facility id that sanitising would alter, and sanitises
separately for logging.

## Reads

`GET` answers `404` whenever the facility has no usable configuration — no row, no authentication on
the row, or another authentication type such as Epic or Basic. A configuration whose client secret no
longer resolves in the vault is still returned, with `clientSecretStored: false`, because the
configuration does exist and that flag is how the caller learns the secret is missing.

The client secret is never returned in any response.
