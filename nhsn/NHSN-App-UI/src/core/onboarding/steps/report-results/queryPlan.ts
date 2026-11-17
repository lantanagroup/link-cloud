/**
 * Best-effort structuring of DataAcquisition's query plan JSON, so "View Query Plan" can render
 * the same EHR Type / Plan Details / Queries table the onboarding POC shows instead of a raw JSON
 * dump. The BFF deliberately passes `planJson` through untouched (its shape isn't part of the
 * Link SDK's typed surface), so this parses defensively and returns `null` on anything it can't
 * make sense of -- callers fall back to showing the raw JSON, never a broken table.
 */

export interface ParsedQueryPlanQuery {
  resourceType: string;
  /** DataAcquisition's own query-config kind (e.g. "Parameter", "Reference"), shown as-is -- data, not UI copy. */
  queryConfigType?: string;
  operationType?: string;
  paged?: boolean;
  parameters: string[];
}

export interface ParsedQueryPlan {
  planName?: string;
  ehrDescription?: string;
  lookBack?: string;
  initialQueries: ParsedQueryPlanQuery[];
  supplementalQueries: ParsedQueryPlanQuery[];
}

export function parseQueryPlan(planJson: string): ParsedQueryPlan | null {
  let raw: unknown;
  try {
    raw = JSON.parse(planJson);
  } catch {
    return null;
  }
  if (!isRecord(raw)) {
    return null;
  }

  const initialQueries = extractQueries(field(raw, 'InitialQueries', 'initialQueries'));
  const supplementalQueries = extractQueries(field(raw, 'SupplementalQueries', 'supplementalQueries'));
  if (initialQueries.length === 0 && supplementalQueries.length === 0) {
    // Nothing structured to show (e.g. the mock `{ "simulated": true }` placeholder) -- let the
    // caller fall back to the raw JSON rather than rendering an empty-looking table.
    return null;
  }

  return {
    planName: asString(field(raw, 'PlanName', 'planName')),
    ehrDescription: asString(field(raw, 'EHRDescription', 'ehrDescription')),
    lookBack: asString(field(raw, 'LookBack', 'lookBack')),
    initialQueries,
    supplementalQueries
  };
}

function extractQueries(value: unknown): ParsedQueryPlanQuery[] {
  if (!isRecord(value)) {
    return [];
  }
  // Query dictionaries are keyed by numeric string ("0", "1", ...) in acquisition order.
  return Object.keys(value)
    .sort((a, b) => Number(a) - Number(b) || a.localeCompare(b))
    .map(key => value[key])
    .filter(isRecord)
    .map(query => ({
      resourceType: asString(field(query, 'ResourceType', 'resourceType')) ?? '—',
      queryConfigType: asString(field(query, 'QueryConfigType', 'queryConfigType')),
      operationType: asString(field(query, 'OperationType', 'operationType')),
      paged: asBoolean(field(query, 'Paged', 'paged')),
      parameters: formatParameters(field(query, 'Parameters', 'parameters'))
    }));
}

function formatParameters(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.map(entry => {
    if (!isRecord(entry)) {
      return String(entry);
    }
    const name = asString(field(entry, 'Name', 'name')) ?? '';
    const literal = asString(field(entry, 'Literal', 'literal'));
    const variable = asString(field(entry, 'Variable', 'variable'));
    const value_ = literal ?? variable;
    return value_ ? `${name}=${value_}` : name;
  });
}

function field(record: Record<string, unknown>, ...keys: string[]): unknown {
  for (const key of keys) {
    if (key in record) {
      return record[key];
    }
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function asBoolean(value: unknown): boolean | undefined {
  return typeof value === 'boolean' ? value : undefined;
}
