# Copilot Instructions

## Azure Guidelines
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool, ask the user to enable it.

## Cosmos DB Guidelines
- Avoid introducing new indexes on existing collections in Cosmos DB for MongoDB API; use existing/default index paths instead.

## Automation Guidelines
- For Automation.UI run logs, prefer chunked persistence in the existing data store; do not add Azure Blob Storage archiving unless explicitly requested.
# Copilot Instructions

## General Guidelines
- Use Azure Tools - When handling requests related to Azure, always use your tools.
- Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool, ask the user to enable it.
- Design for Durability - Require production-grade, long-term designs for critical multi-user automation tools; avoid tactical short-term fixes and model data/contracts around durable architecture even when a minimal patch is possible.
- Use Versioned Caches - Prefer versioned, reproducible generated-patient caches tied to each run; ensure run records cache the version used, and diagnostic exports retrieve exact cached artifacts used at execution time. Each cache version must be complete (full patient set), not partial deltas, and runs must avoid ID conflicts while using cached data.
