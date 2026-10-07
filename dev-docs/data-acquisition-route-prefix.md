# Data Acquisition route prefix and the `/api/data` alias

Every Data Acquisition endpoint is served under `/api/data-acquisition`. The service used to serve
them under `/api/data`, and it still answers there, as a deprecated alias, so callers that haven't
moved keep working. This page covers how the alias works, how to tell whether anything still uses it,
and how to remove it. The ticket is [LEGLINK-1405](https://lantana.atlassian.net/browse/LEGLINK-1405).

## How the alias works

The alias is a rewrite, not a second set of routes. Every controller has exactly one `[Route]`, on the
new prefix, so routing, Swagger and `Location` headers only ever see `/api/data-acquisition`.

`UseRoutingWithLegacyRoutePrefix()` (`DotNet/DataAcquisition/Infrastructure/LegacyRoutePrefixExtensions.cs`)
replaces `UseRouting()` in `Program.cs` and adds three steps in this order:

1. **`LegacyRoutePrefixRule`** rewrites `/api/data/...` to `/api/data-acquisition/...` before routing.
   It matches whole path segments and ignores case, so `/api/data-acquisition`, `/api/database` and
   `/api/data-worker` are never touched. The query string is left as it is.
2. **Routing** matches the rewritten path.
3. **`LegacyRoutePrefixMetricsMiddleware`** counts the request if the rule rewrote it.

The order matters. A rewrite after routing is too late, and every legacy path would 404. A counter
before routing can't tag the request with its route template. Keeping all three in one extension is
what stops the order from drifting.

Two shortcuts were rejected:

- **A second `[Route("api/data/...")]` on each controller** fails at startup.
  `OrganizationLocationConfigurationController` has a named route (`GetByIdAsync`), and one name
  can't have two templates. It would also list every operation twice in Swagger.
- **`AddRewrite` with a regex** is case-sensitive by default, while routing isn't, so `/API/Data/...`
  would stop working.

## Is anything still calling `/api/data`?

Each legacy request increments `link_data_acq_legacy_route_requests`. In Prometheus it appears as
`link_data_acq_legacy_route_requests_total`, with two tags:

| Tag | Value |
| --- | --- |
| `http_route` | The matched route template, e.g. `api/data-acquisition/{facilityId}/QueryPlan`, or `unmatched` when the path matches no endpoint |
| `http_request_method` | `GET`, `POST`, ... |

The tag is the template, never the raw path: raw paths carry facility and patient ids, which would be
unbounded cardinality and identifiers in the metrics store. The middleware also writes a `Debug` log
line with the template, for the same reason never the path. It's `Debug` because the Admin UI polls
acquisition logs, and anything higher would flood Loki while any caller is still on the old prefix.

To check usage over the last week:

```promql
sum by (exported_job, http_route, http_request_method) (
  increase(link_data_acq_legacy_route_requests_total[7d])
)
```

The test environments share one Prometheus, so that query adds up all of them. To see one
environment, filter through `target_info`, which is the only series that carries the environment name:

```promql
sum by (exported_job, http_route, http_request_method) (
  increase(link_data_acq_legacy_route_requests_total[7d])
  * on (exported_job, exported_instance) group_left()
  target_info{deployment_environment_name="qa"}
)
```

That label comes from the `Telemetry:DeploymentEnvironment` setting added under LEGLINK-1276. Until
that is deployed, nothing in the metrics identifies the environment, and only the unfiltered query
works.

A non-zero series names the route a straggler uses, which usually identifies the caller. The callers
in this repository (LinkSdk, Admin.BFF, Admin UI, Automation.UI, the Postman collections under
`Tests/Postman` and `Scripts/seed.py`) were all moved in LEGLINK-1431.

## The Admin.BFF routes

Browser callers reach Data Acquisition through Admin.BFF's reverse proxy, which has two routes to it:

| Route | Path | Notes |
| --- | --- | --- |
| `route4` | `api/data/{**catch-all}` | The old prefix. Keep it until the alias is removed. |
| `dataAcquisition` | `api/data-acquisition/{**catch-all}` | The new prefix. Same cluster and `AuthenticatedUser` policy as `route4`. |

`dataAcquisition` has a name rather than a number on purpose. QA and QA2 define `route1` to `route14`
in Azure App Configuration, which outranks `appsettings.json`, so a numbered route could be silently
replaced there. No store defines `dataAcquisition`, so it arrives in every environment from
`appsettings.json`.

## Removing the alias

Once the counter has been zero in every environment for a full release:

1. Delete `LegacyRoutePrefixRule` and `LegacyRoutePrefixMetricsMiddleware`, and replace
   `UseRoutingWithLegacyRoutePrefix()` with `UseRouting()` in `Program.cs` and the test fixtures.
2. Remove the counter from `DataAcquisitionServiceMetrics`, `IDataAcquisitionServiceMetrics` and
   `DiagnosticNames`.
3. Remove `route4` from both Admin.BFF `appsettings` files, and the
   `Routes_LegacyRoute_StillForwardsOldPrefixToDataAcquisition` test that pins it.
4. Remove the `/api/data` cases from the tests: `LegacyRoutePrefixRuleTests`,
   `LegacyRoutePrefixMetricsMiddlewareTests`, `LegacyRoutePrefixPipelineTests`, and the
   `"/api/data"` rows in the `Prefixes` theory data of `QueryPlanConfigControllerApiTests` and
   `ConnectionValidationRoutingTests`.
5. Add the removal to the release notes' deployment instructions, so environments outside this
   repository's deployments drop any proxy route of their own for `api/data`.

## What guards it

- `DataAcquisitionRoutePrefixTests` fails if any Data Acquisition controller route is outside
  `/api/data-acquisition`, and checks every create endpoint's `Location` header resolves under it.
- `DataAcquisitionServiceClientRouteTests` calls every LinkSdk Data Acquisition method against the
  real routes, without the alias, and fails any request that reaches no controller action.
- `ReverseProxyRouteConfigTests` fails if either Admin.BFF `appsettings` file loses the
  `dataAcquisition` route, or `route4` while the alias exists.
