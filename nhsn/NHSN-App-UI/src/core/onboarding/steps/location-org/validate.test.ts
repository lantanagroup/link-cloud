import {describe, expect, it} from 'vitest';
import {
  findDuplicateLocationIdentifierIndexes,
  findDuplicateLocationTypeIndexes,
  findDuplicateManagingOrganizationIndexes
} from './validate';

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

describe('findDuplicateManagingOrganizationIndexes', () => {
  it('flags every repeat of a value after the first, ignoring case and whitespace', () => {
    const rows = ['Org/123', 'Org/456', ' org/123 ', 'Org/123'];
    expect(findDuplicateManagingOrganizationIndexes(rows)).toEqual([2, 3]);
  });

  it('puts the error on the row being edited rather than the one already there', () => {
    const rows = ['Org/123', 'Org/123'];
    expect(findDuplicateManagingOrganizationIndexes(rows, 0)).toEqual([0]);
  });

  it('skips blank rows', () => {
    const rows = ['', '', 'Org/123'];
    expect(findDuplicateManagingOrganizationIndexes(rows)).toEqual([]);
  });
});
