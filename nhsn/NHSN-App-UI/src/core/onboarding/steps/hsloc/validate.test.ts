import {describe, expect, it} from 'vitest';
import {findDuplicateSourceCodeIndexes, findIncompleteRowIndexes, isRowBlank, isRowComplete} from './validate';

describe('isRowBlank', () => {
  it('is true when every field is empty or whitespace', () => {
    expect(isRowBlank({sourceDisplay: '', sourceCode: '  ', hslocCode: ''})).toBe(true);
  });

  it('is false once any single field has a value', () => {
    expect(isRowBlank({sourceDisplay: 'ICU', sourceCode: '', hslocCode: ''})).toBe(false);
  });
});

describe('isRowComplete', () => {
  it('is true only when all three fields have a value', () => {
    expect(isRowComplete({sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'})).toBe(true);
  });

  it('is false when any field is blank', () => {
    expect(isRowComplete({sourceDisplay: 'ICU', sourceCode: '', hslocCode: '1030'})).toBe(false);
  });
});

describe('findIncompleteRowIndexes', () => {
  it('flags rows missing any field', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'},
      {sourceDisplay: '', sourceCode: 'ED-1', hslocCode: '1002'},
      {sourceDisplay: 'ED', sourceCode: '', hslocCode: '1002'},
      {sourceDisplay: 'ED', sourceCode: 'ED-1', hslocCode: ''}
    ];
    expect(findIncompleteRowIndexes(rows)).toEqual([1, 2, 3]);
  });

  it('treats whitespace-only values as missing', () => {
    const rows = [{sourceDisplay: '   ', sourceCode: 'ED-1', hslocCode: '1002'}];
    expect(findIncompleteRowIndexes(rows)).toEqual([0]);
  });

  it('passes a fully populated row', () => {
    const rows = [{sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'}];
    expect(findIncompleteRowIndexes(rows)).toEqual([]);
  });
});

describe('findDuplicateSourceCodeIndexes', () => {
  it('keeps the first row clean and flags the rest when no row is being edited', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'},
      {sourceDisplay: 'ED', sourceCode: 'ED-1', hslocCode: '1002'},
      {sourceDisplay: 'ICU Alt', sourceCode: 'ICU-1', hslocCode: '1031'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([2]);
  });

  it('flags the row being edited, leaving the other occurrence clean', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'},
      {sourceDisplay: 'ED', sourceCode: 'ED-1', hslocCode: '1002'},
      {sourceDisplay: 'ICU Alt', sourceCode: 'ICU-1', hslocCode: '1031'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows, 0)).toEqual([0]);
  });

  it('compares trimmed and case-insensitively, matching the save path', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: ' icu-1 ', hslocCode: '1030'},
      {sourceDisplay: 'ICU Alt', sourceCode: 'ICU-1', hslocCode: '1031'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([1]);
  });

  it('ignores blank source codes, which findIncompleteRowIndexes already flags', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: '', hslocCode: '1030'},
      {sourceDisplay: 'ED', sourceCode: '', hslocCode: '1002'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([]);
  });

  it('passes rows with no collisions', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'},
      {sourceDisplay: 'ED', sourceCode: 'ED-1', hslocCode: '1002'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([]);
  });
});
