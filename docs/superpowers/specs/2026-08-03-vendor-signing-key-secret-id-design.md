# Vendor-Level Signing Key Association (Secret ID) — UI Design

- **Ticket:** LEGLINK-620 (Dev - UI Changes), subtask of LEGLINK-63 (UI Support for Associating JWKS Keys to Vendor)
- **Date:** 2026-08-03
- **Status:** Approved, pending backend contract

## Background

Link signs the JWT that Data Acquisition presents to external EHR systems (Epic, Cerner) during
client-credentials authentication. The PEM signing key used to live in the database on the
facility's authentication configuration. LEGLINK-14 moved it into Azure Key Vault, stored as a
secret.

LEGLINK-63 makes the key association explicit and vendor-scoped: each vendor points at the Key
Vault secret holding its PEM. Vendor-level (rather than facility-level) is deliberate — at least
one vendor, Veradigm, needs a different key generation algorithm than Epic and Cerner, so the keys
cannot be shared.

The stored value is the **Key Vault secret ID** — the name of the secret entry — not the JWKS `kid`
header value. LEGLINK-63's original wording conflated the two; the 6/30/2026 refinement meeting
carried an action item to correct that language, and the sibling ticket LEGLINK-567 ("Use
Vendor-Secret ID Association for Key Retrieval") uses the accurate term.

## Current state

**Backend.** The `Vendor` entity lives in the Normalization service
(`DotNet/Normalization/Domain/Managers/VendorManager.cs`) and carries only `Id` and `Name`.
`VendorController` exposes GET / POST / DELETE — there is no update operation. Separately, the
Tenant service has an unrelated `Vendor` *enum* (`Epic`, `Cerner`) on `Facility`.

`EpicAuth.ResolvePem` (`DotNet/DataAcquisition.Domain/Application/Services/Auth/EpicAuth.cs`)
currently derives the secret name by convention as `{facilityId}-pem` when
`DataSourceAuth:KeySource` is `SecretManager`, falling back to the database-stored PEM on
`AuthenticationConfiguration.Key` when it is `Database`. It sets no `kid` header on the JWT.

**Frontend.** `Web/Admin.UI/src/app/components/vendor/` holds a dashboard (list, create, delete),
a dialog, and a form. The form handles `name` only and only in `FormMode.Create`. The dialog
already renders Edit-mode affordances ("Update Vendor Configuration") but is never opened that
way. `MatExpansionModule` is imported into the form component and unused.

## Constraints

LEGLINK-620 is sequenced after LEGLINK-743 (Move Vendor Model Out of Normalization and Into
Tenant, in progress). LEGLINK-743's acceptance criteria cover list, add, and delete — **not
update** — so the endpoint that persists a secret ID is unowned by either ticket. This work
therefore proceeds UI-first against a contract to be confirmed.

## Scope

**In scope**

- An edit path in the Vendor Management UI
- A JWT / Authentication section holding the Key Vault Secret ID
- Unit specs and a mocked Playwright end-to-end spec

**Deferred, blocked on backend**

- The persistence call itself
- LEGLINK-63 AC #3, "Changes trigger an audit trail event." Audit events are produced from
  backend managers onto Kafka (for example `DotNet/Tenant/Commands/CreateAuditEventCommand.cs`
  driving `FacilityManager`). No UI change can satisfy this AC; it belongs with the vendor update
  endpoint. Noted on LEGLINK-620 as blocked-on-backend.

**Out of scope (other tickets)**

- Validating an entered secret ID against Key Vault — LEGLINK-566
- Key retrieval and usage changes in Data Acquisition — LEGLINK-567
- Tenant and facility level key overrides — future enhancement
- Displaying key status (active, expired, disabled)

## Data model

`IVendorConfigModel` gains one optional field:

```ts
export interface IVendorConfigModel {
  id: string;
  name: string;
  secretId?: string;   // Key Vault secret name holding the PEM signing key
}
```

The field is optional because every existing vendor record has no secret ID. It is free text with
no format validation; validation against Key Vault is LEGLINK-566.

## UI design

Dashboard, with the secret ID surfaced in the list so admins can see coverage at a glance:

```
Vendors                                    [+]
---------------------------------------------
 Name        Secret ID           Actions
 Epic        epic-signing-pem    [edit] [del]
 Cerner      (not set)           [edit] [del]
```

Edit dialog, with the key settings in their own collapsible section per LEGLINK-63's "a section
exists for JWT/Auth settings":

```
 Vendor Configuration

  Name  [ Epic                            ]

  v JWT / Authentication
      Key Vault Secret ID
      [ epic-signing-pem                  ]
      Name of the Key Vault secret holding
      the PEM signing key.

        [ Update Vendor Configuration ] [Close]
```

The panel starts expanded when the vendor already has a secret ID and collapsed when it does not,
so existing configuration is visible without hunting.

### Component changes

| File | Change |
| --- | --- |
| `vendor-dashboard.component.html` / `.ts` | Add a `secretId` column rendering "Not set" when empty; add a per-row edit action; `onEdit()` opens the dialog with `FormMode.Edit` and the row; refresh the list and show a snackbar on successful close |
| `vendor-config-form.component.html` | Add a `mat-expansion-panel` titled "JWT / Authentication" containing the Key Vault Secret ID field and its hint text |
| `vendor-config-form.component.ts` | Add the `secretId` control, populate it from `item` in `ngOnInit`, and add the `FormMode.Edit` branch to `submitConfiguration()` |
| `vendor-config-dialog.*` | No change; Edit mode is already wired |

## The contract seam

Every change above is independent of the backend contract. The single unknown is isolated to one
service method:

```ts
updateVendor(vendor: IVendorConfigModel): Observable<IApiResponse>
```

When the route and payload are confirmed with the LEGLINK-743 work, that method is the only edit
required. A config-flagged dual path (Normalization before the move, Tenant after) was considered
and rejected: it adds a permanent branch to buy insurance against a decision expected within days.

Until the contract lands, `updateVendor` targets a documented placeholder route and is exercised
through mocks.

### Incidental fixes

Two defects in files this work already touches:

- `VendorService.createVendor(vendorId: IVendorConfigModel)` is typed as a model but is passed a
  name string and interpolated directly into the URL.
- `VendorDashboardComponent.getVendors()` never clears `loading` on the success path.

## Error handling

Save failures propagate through the existing `ErrorHandlingService`, causing
`submittedConfiguration` to emit `{success: false, message}`. The dialog then shows an error
snackbar and stays open, preserving the admin's input. This path already exists for Create; the
Edit branch reuses it rather than introducing a second mechanism.

## Testing

**Form spec** (`vendor-config-form.component.spec.ts`, new)

- Populates `secretId` from the `item` input in Edit mode
- Name remains required; secret ID does not
- Submits successfully both with and without a secret ID
- Emits `{success: false}` and does not close on a failed save

**Dashboard spec** (`vendor-dashboard.component.spec.ts`, extended)

- The edit action opens the dialog with `FormMode.Edit` and the row's data
- The list refreshes after a successful save
- A vendor with no secret ID renders "Not set"

**Mocked Playwright spec** (`Web/Admin.UI/e2e`)

- Navigate to Vendors, edit a vendor, enter a secret ID, save, and assert the row reflects the new
  value, with the update endpoint stubbed through the existing ApiMock fixtures

The mocked tier lets the full flow be verified before the endpoint exists, and becomes the
regression net for LEGLINK-569 (QA: Validate Vendor-Level KID Association).

## Open items

1. Update endpoint route and payload, to be confirmed with the LEGLINK-743 work.
2. Ownership of the audit trail event for vendor changes (LEGLINK-63 AC #3).
