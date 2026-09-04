import {describe, expect, it} from 'vitest';
import {getStep} from '../../flow';
import {createEmptyDraft} from '../../types';
import {CENSUS_LIST_KEYS} from './validate';

const isComplete = getStep('census')!.isComplete;

describe('census step completion', () => {
  it('is incomplete on an empty draft', () => {
    expect(isComplete(createEmptyDraft())).toBe(false);
  });

  it('is incomplete once configured but not yet acknowledged', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(isComplete(draft)).toBe(false);
  });

  it('is complete for an Epic-shaped draft once acknowledged', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft)).toBe(true);
  });

  it('is complete for a Cerner-shaped draft once acknowledged', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT1H0M';
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft)).toBe(true);
  });

  it('stays incomplete if the acknowledged frequency is under 15 minutes', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H5M';
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft)).toBe(false);
  });
});
