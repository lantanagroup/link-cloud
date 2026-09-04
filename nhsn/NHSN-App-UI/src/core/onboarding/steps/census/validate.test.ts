import {describe, expect, it} from 'vitest';
import {createEmptyDraft} from '../../types';
import {CENSUS_LIST_KEYS, validateCensus} from './validate';

describe('validateCensus', () => {
  it('requires no fields when the acquisition method is unknown', () => {
    expect(validateCensus(createEmptyDraft(), undefined)).toEqual({});
  });

  it('requires all six patient list ids for Epic', () => {
    const errors = validateCensus(createEmptyDraft(), 'PatientList');
    CENSUS_LIST_KEYS.forEach(key => {
      expect(errors[`listId.${key}`]).toBeDefined();
    });
  });

  it('passes Epic once every list id and a valid frequency are set', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(validateCensus(draft, 'PatientList')).toEqual({});
  });

  it('requires host and port for Cerner', () => {
    const errors = validateCensus(createEmptyDraft(), 'Sftp');
    expect(errors.sftpHost).toBeDefined();
    expect(errors.sftpPort).toBeDefined();
  });

  it('rejects a port outside the valid range', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 70000;
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(validateCensus(draft, 'Sftp').sftpPort).toBeDefined();
  });

  it('passes Cerner once host, port and a valid frequency are set', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(validateCensus(draft, 'Sftp')).toEqual({});
  });

  it('rejects a frequency under 15 minutes', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H10M';
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeDefined();
  });

  it('rejects a missing frequency', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeDefined();
  });
});
