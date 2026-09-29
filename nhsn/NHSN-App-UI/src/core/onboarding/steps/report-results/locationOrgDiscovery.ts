import type { LocationOrgMapping } from '../../../api/contracts';

interface FhirResourceLike {
  resourceType: string;
  id: string;
  [key: string]: unknown;
}

export function parseNdjsonResources(text: string): FhirResourceLike[] {
  const resources: FhirResourceLike[] = [];
  for (const line of text.split('\n')) {
    const trimmed = line.trim();
    if (!trimmed) {
      continue;
    }
    try {
      const parsed = JSON.parse(trimmed);
      if (
        parsed &&
        typeof parsed === 'object' &&
        typeof parsed.resourceType === 'string' &&
        typeof parsed.id === 'string'
      ) {
        resources.push(parsed as FhirResourceLike);
      }
    } catch {
      continue;
    }
  }
  return resources;
}

export interface LocationRawValues {
  locationTypeCodes: string[];
  alias?: string;
  identifiers: Array<{ system?: string; value?: string }>;
  managingOrganizationId?: string;
}

export interface LocationDiscoveryRow {
  locationId: string;
  found: boolean;
  raw: LocationRawValues | null;
}

function splitReference(reference: string | undefined): string | undefined {
  if (!reference) {
    return undefined;
  }
  const separatorIndex = reference.indexOf('/');
  return separatorIndex === -1 ? reference : reference.slice(separatorIndex + 1);
}

function extractRawValues(location: FhirResourceLike): LocationRawValues {
  const typeCodings = ((location.type as Array<{ coding?: Array<{ code?: string }> }>) ?? []).flatMap(
    (entry) => entry.coding ?? [],
  );
  const identifiers = (location.identifier as Array<{ system?: string; value?: string }> | undefined) ?? [];
  const managingOrganizationId = splitReference(
    (location.managingOrganization as { reference?: string } | undefined)?.reference,
  );

  return {
    locationTypeCodes: typeCodings.map((coding) => coding.code).filter((code): code is string => Boolean(code)),
    alias: (location.alias as string[] | undefined)?.[0],
    identifiers: identifiers.map((identifier) => ({ system: identifier.system, value: identifier.value })),
    managingOrganizationId,
  };
}

/**
 * Every Location id referenced by the patient's encounters (direct + `partOf` ancestors), joined
 * against the facility's own mapping list for found/missing. Raw field values are only available
 * for a Location that is actually present as a resource in `resources` -- an ancestor reached only
 * through a `partOf` reference is often not, since the exported report only bundles what CQL
 * touched during evaluation, not the full Location ancestry chain.
 */
export function discoverLocationOrgRows(
  resources: FhirResourceLike[],
  mappings: LocationOrgMapping[],
): LocationDiscoveryRow[] {
  const locationsById = new Map<string, FhirResourceLike>();
  for (const resource of resources) {
    if (resource.resourceType === 'Location') {
      locationsById.set(resource.id, resource);
    }
  }

  const mappingsByLocationId = new Map(mappings.map((mapping) => [mapping.locationId, mapping]));

  const referencedIds = new Set<string>();
  for (const resource of resources) {
    if (resource.resourceType !== 'Encounter') {
      continue;
    }
    const locationEntries = (resource.location as Array<{ location?: { reference?: string } }> | undefined) ?? [];
    for (const entry of locationEntries) {
      const locationId = splitReference(entry.location?.reference);
      if (locationId) {
        referencedIds.add(locationId);
      }
    }
  }

  const allIds = new Set(referencedIds);
  const queue = [...referencedIds];
  while (queue.length > 0) {
    const locationId = queue.shift()!;
    const parentId = splitReference(
      (locationsById.get(locationId)?.partOf as { reference?: string } | undefined)?.reference,
    );
    if (parentId && !allIds.has(parentId)) {
      allIds.add(parentId);
      queue.push(parentId);
    }
  }

  return [...allIds].map((locationId) => {
    const location = locationsById.get(locationId);
    return {
      locationId,
      found: mappingsByLocationId.get(locationId)?.isOrgLocation ?? false,
      raw: location ? extractRawValues(location) : null,
    };
  });
}
