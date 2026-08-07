# Copilot Instructions

## Azure Guidelines
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool, ask the user to enable it.

## Cosmos DB Guidelines
- Avoid introducing new indexes on existing collections in Cosmos DB for MongoDB API; use existing/default index paths instead.

## Automation Guidelines
- For Automation.UI run logs, prefer chunked persistence in the existing data store; do not add Azure Blob Storage archiving unless explicitly requested.
