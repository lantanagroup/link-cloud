import {describe, expect, it} from 'vitest';
import {isPlausibleFhirPath} from './validate';

describe('isPlausibleFhirPath', () => {
  it.each([
    '',
    '   ',
    "Location.identifier.where(system = 'urn:oid:1.2.3' and value = 'ABC').exists()",
    "Location.managingOrganization.reference = 'Organization/123'",
    "Location.alias.contains('Main (North) Campus.')",
    'Location.extension[0].value',
    'Location.position.latitude > 40.5',
    '%resource.id'
  ])('accepts %j', value => {
    expect(isPlausibleFhirPath(value)).toBe(true);
  });

  it.each([
    'jhgfdxcvbnm.',
    '.identifier',
    'Location..identifier',
    'Location.(identifier)',
    "Location.alias.contains('Main",
    'Location.identifier.where(system = 1',
    'Location.identifier)',
    'Location.id =',
    'Location.id; drop'
  ])('rejects %j', value => {
    expect(isPlausibleFhirPath(value)).toBe(false);
  });
});
