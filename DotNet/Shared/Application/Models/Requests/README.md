# Paging requests

Use `[FromQuery] PagingRequest paging` on paged MVC actions, or inherit from
`PagingRequest` in an HTTP search request DTO. Controllers must use `[ApiController]`
so invalid model state automatically returns HTTP 400 Validation Problem Details.
No custom filter, binder, or service registration is needed.

`PageSize` defaults to 10 and uses `[Range(1, 100)]`. Values outside that range fail
validation. Non-integers, empty values, and integer overflow fail standard integer
model binding. Error messages are supplied by ASP.NET rather than custom code.
Repeated scalar parameters follow normal ASP.NET binding behavior (first value).

`PageNumber` defaults to 1. This initial adoption does not add page-number range
validation or change existing query-layer handling of page numbers.

The property initializers provide runtime defaults; `[DefaultValue]` exposes those
defaults to OpenAPI tooling, and `[Range]` supplies the page-size bounds. Document
HTTP 400 in the action's response metadata. Use the bound paging values for query
offsets, limits, and response metadata without silently replacing valid sizes.

Keep API request DTOs separate from internal search models when internal callers
need different paging behavior. The first adopter is Normalization's
facility-location local-code mappings controller. Other controllers are unchanged.

Test validation through HTTP: direct controller calls do not execute model binding
or automatic validation. Standard prefixed query binding is supported and validated
as well; no special empty-prefix annotation is required.