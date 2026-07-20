# Postman collections

Manual/local API test collections for the Link platform. Import a `*.postman_collection.json` file into Postman (or run it with the Newman CLI).

## inactive-code-validation.postman_collection.json

End-to-end check that an **inactive** code surfaces as a validation `Result` and is categorized as a `missing_active_*` category (LEGLINK-580).

Run the 9 requests top-to-bottom. Each has test assertions and logs details to the Postman console; request **9** is the actual test (`$validate?categorize=true`) and, on failure, dumps every validation message so you can see what HAPI produced.

### Collection variables

Set these on the collection's **Variables** tab before running:

| Variable | Purpose | Local default |
| --- | --- | --- |
| `validationBaseUrl` | Validation service base URL | `http://localhost:8075` |
| `terminologyBaseUrl` | Terminology service base URL | `http://localhost:8076` |
| `codeSystem` / `codeSystemId` / `code` / `codeDisplay` | An **inactive** code known to the terminology service | SNOMED `423666004` |
| `resourceType` / `profileUrl` | The resource + profile the code sits on (drives request 9's bundle) | `Encounter` / `us-core-encounter` |
| `bindingValueSetUrl` | The value set bound to that element (drives request 9's bundle) | `http://hl7.org/fhir/us/core/ValueSet/detailed-race` |
| `expectedCategoryId` | The category the finding should map to | `missing_active_encounter_type_code` |

The sample bundle in request 9 places the code on `Encounter.type`. If your loaded inactive code sits on a different element, update the variables and edit request 9's body to match.

### Local prerequisite — wire validation to the terminology service

In the deployed environments `link.terminology-service-url` comes from Azure App Configuration, but the **local docker stack has App Configuration disabled**, so the validation service falls back to in-memory terminology and never calls the terminology service. Wire it with a local `docker-compose.override.yml` (do **not** commit it) at the repo root:

```yaml
services:
  validation:
    environment:
      link.terminology-service-url: http://terminology:8076
```

Then `docker compose up -d validation` to restart it. Against the **test env** this override is unnecessary — point `validationBaseUrl`/`terminologyBaseUrl` at the test hosts and skip requests 3-4.

### What passing/failing tells you

- **Request 5 passes, request 9 fails (no inactive result):** the terminology service flagged the code, but HAPI did not surface the warning as a `Result` — the element's binding didn't route to the terminology service (wrong/weak binding) or the WARNING was not propagated. The all-messages dump in request 9's console output shows what HAPI actually produced.
- **Request 9 passes:** the inactive finding surfaces and categorizes end-to-end.
