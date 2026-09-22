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

  it('passes Epic with a minutes-only frequency at the 5 minute floor, hours not required', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H5M';
    expect(validateCensus(draft, 'PatientList').acquisitionFrequency).toBeUndefined();
  });

  it('passes Epic with an hours-only frequency, minutes not required', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT2H0M';
    expect(validateCensus(draft, 'PatientList').acquisitionFrequency).toBeUndefined();
  });

  it('rejects an Epic frequency under 5 minutes', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H4M';
    expect(validateCensus(draft, 'PatientList').acquisitionFrequency).toBeDefined();
  });

  it('rejects an Epic frequency over 24 hours', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT25H0M';
    expect(validateCensus(draft, 'PatientList').acquisitionFrequency).toBeDefined();
  });

  it('rejects a missing Epic frequency even with all list ids set', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    expect(validateCensus(draft, 'PatientList').acquisitionFrequency).toBeDefined();
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

  it('rejects a host over 128 characters', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = `${'a'.repeat(122)}.invalid`;
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(validateCensus(draft, 'Sftp').sftpHost).toBe('onboarding:census.errors.hostTooLong');
  });

  it('passes Cerner once host, port and a valid frequency are set', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(validateCensus(draft, 'Sftp')).toEqual({});
  });

  it('rejects a frequency under 5 minutes', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H4M';
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeDefined();
  });

  it('rejects a frequency over 24 hours', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT25H0M';
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeDefined();
  });

  it('passes a minutes-only frequency at the 5 minute floor, hours not required', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H5M';
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeUndefined();
  });

  it('passes an hours-only frequency, minutes not required', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT2H0M';
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeUndefined();
  });

  it('rejects a missing frequency', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    expect(validateCensus(draft, 'Sftp').acquisitionFrequency).toBeDefined();
  });
});
