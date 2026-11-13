import type { HslocMapping } from '../../../api/contracts';
import { resolveReferencedLocationIds, typeCodingsOf, type FhirCoding, type FhirResourceLike } from './locationOrgDiscovery';

const HSLOC_CODESYSTEM_URL = 'https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html';

export interface HslocDiscoveredCoding {
  key: string;
  locationId: string;
  system?: string;
  code: string;
  display?: string;
}

export interface HslocDiscoveryRow extends HslocDiscoveredCoding {
  found: boolean;
  matchedHslocCode?: string;
  matchedSourceDisplay?: string;
}

function codingsFromLocation(location: FhirResourceLike): FhirCoding[] {
  const physicalTypeCodings = (location.physicalType as { coding?: FhirCoding[] } | undefined)?.coding ?? [];
  return [...typeCodingsOf(location), ...physicalTypeCodings];
}

// Not joined against mappings -- see resolveHslocDiscoveryRows -- so a save can re-resolve
// without re-parsing the export. Scoped to Locations this patient's own encounters actually
// reference (see resolveReferencedLocationIds) -- otherwise a bundle carrying Location resources
// unrelated to this patient would surface someone else's HSLOC codes as this patient's.
export function discoverHslocCodings(resources: FhirResourceLike[]): HslocDiscoveredCoding[] {
  const referencedIds = resolveReferencedLocationIds(resources);
  const codings: HslocDiscoveredCoding[] = [];
  const seen = new Set<string>();

  for (const resource of resources) {
    if (resource.resourceType !== 'Location' || !referencedIds.has(resource.id)) {
      continue;
    }
    for (const coding of codingsFromLocation(resource)) {
      if (!coding.code || coding.system === HSLOC_CODESYSTEM_URL) {
        continue;
      }
      const key = `${coding.system ?? ''}|${coding.code}`;
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);
      codings.push({ key, locationId: resource.id, system: coding.system, code: coding.code, display: coding.display });
    }
  }

  return codings;
}

// Matches on sourceCode -- the hsloc-mappings row's real key (HslocMappingService.cs stores the
// vendor code as SourceCode and uses it as a stand-in Location id, not the other way around).
export function resolveHslocDiscoveryRows(
  codings: HslocDiscoveredCoding[],
  mappings: HslocMapping[],
): HslocDiscoveryRow[] {
  const mappingBySourceCode = new Map(mappings.map((mapping) => [mapping.sourceCode, mapping]));
  return codings.map((coding) => {
    const match = mappingBySourceCode.get(coding.code);
    return {
      ...coding,
      found: Boolean(match),
      matchedHslocCode: match?.hslocCode,
      matchedSourceDisplay: match?.sourceDisplay,
    };
  });
}

// Empty is true here -- unlike evidence.codeMaps, an empty discovery result means "checked,
// nothing outstanding", not "never evaluated".
export function isHslocDiscoveryFullyMapped(
  codings: HslocDiscoveredCoding[],
  mappings: HslocMapping[],
): boolean {
  const mappedCodes = new Set(mappings.map((mapping) => mapping.sourceCode));
  return codings.every((coding) => mappedCodes.has(coding.code));
}
