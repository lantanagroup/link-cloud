import {describe, expect, it} from 'vitest';
import {findDuplicateLocationIdentifierIndexes, findDuplicateLocationTypeIndexes, isPlausibleFhirPath} from './validate';

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

describe('findDuplicateLocationIdentifierIndexes', () => {
  it('flags every repeat of a system + code pair after the first, ignoring case and whitespace', () => {
    const rows = [
      {system: 'urn:oid:1.2.3', code: 'ABC'},
      {system: 'urn:oid:1.2.3', code: 'DEF'},
      {system: ' URN:OID:1.2.3 ', code: 'abc '},
      {system: 'urn:oid:1.2.3', code: 'ABC'}
    ];
    expect(findDuplicateLocationIdentifierIndexes(rows)).toEqual([2, 3]);
  });

  it('puts the error on the row being edited rather than the one already there', () => {
    const rows = [
      {system: 'urn:oid:1.2.3', code: 'ABC'},
      {system: 'urn:oid:1.2.3', code: 'ABC'}
    ];
    expect(findDuplicateLocationIdentifierIndexes(rows, 0)).toEqual([0]);
  });

  it('treats the same code under a different system as unique, and skips incomplete rows', () => {
    const rows = [
      {system: 'urn:oid:1.2.3', code: 'ABC'},
      {system: 'urn:oid:9.9.9', code: 'ABC'},
      {system: '', code: 'ABC'},
      {system: '', code: 'ABC'}
    ];
    expect(findDuplicateLocationIdentifierIndexes(rows)).toEqual([]);
  });
});

describe('findDuplicateLocationTypeIndexes', () => {
  it('flags a row repeating the code + alias of another row, but not a shared code alone', () => {
    const rows = [
      {code: '1', alias: '1'},
      {code: '1', alias: '1'},
      {code: '1', alias: '2'}
    ];
    expect(findDuplicateLocationTypeIndexes(rows)).toEqual([1]);
  });
});
