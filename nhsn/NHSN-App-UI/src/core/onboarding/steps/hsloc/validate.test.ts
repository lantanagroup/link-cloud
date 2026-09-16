import {describe, expect, it} from 'vitest';
import {findDuplicateSourceCodeIndexes, findIncompleteRowIndexes} from './validate';

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
  it('flags both rows sharing the same source code', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: 'ICU-1', hslocCode: '1030'},
      {sourceDisplay: 'ED', sourceCode: 'ED-1', hslocCode: '1002'},
      {sourceDisplay: 'ICU Alt', sourceCode: 'ICU-1', hslocCode: '1031'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([0, 2]);
  });

  it('compares trimmed and case-insensitively, matching the save path', () => {
    const rows = [
      {sourceDisplay: 'ICU', sourceCode: ' icu-1 ', hslocCode: '1030'},
      {sourceDisplay: 'ICU Alt', sourceCode: 'ICU-1', hslocCode: '1031'}
    ];
    expect(findDuplicateSourceCodeIndexes(rows)).toEqual([0, 1]);
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
