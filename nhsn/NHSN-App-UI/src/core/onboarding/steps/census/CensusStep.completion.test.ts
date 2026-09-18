import {describe, expect, it} from 'vitest';
import type {Capabilities, UserInfoResponse} from '../../../api/contracts';
import {getStep} from '../../flow';
import {createEmptyDraft} from '../../types';
import {CENSUS_LIST_KEYS} from './validate';

const isComplete = getStep('census')!.isComplete;

function userWith(capabilities: Partial<Capabilities>): UserInfoResponse {
  return {
    accessState: 'Allowed',
    email: 'a@example.invalid',
    name: 'A',
    isFacilityAdmin: true,
    isOnboarded: false,
    hasFacility: true,
    facilityId: 'F1',
    groups: ['FACADMIN'],
    availableNavigation: ['onboarding'],
    capabilities: {
      patientListWithNames: false,
      fhirConnectionProbe: false,
      sftpFileListing: false,
      ...capabilities
    }
  };
}

describe('census step completion', () => {
  it('is incomplete on an empty draft', () => {
    expect(isComplete(createEmptyDraft())).toBe(false);
  });

  it('is incomplete once configured but not yet acknowledged, when validation is live', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(isComplete(draft, userWith({patientListWithNames: true}))).toBe(false);
  });

  it('is complete once configured, with no acknowledgement required, when validation is not live', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    expect(isComplete(draft, userWith({patientListWithNames: false}))).toBe(true);
    expect(isComplete(draft)).toBe(true);
  });

  it('is complete for an Epic-shaped draft once acknowledged', () => {
    const draft = createEmptyDraft();
    draft.census.patientListIds = Object.fromEntries(CENSUS_LIST_KEYS.map(key => [key, `list-${key}`]));
    draft.census.acquisitionFrequency = 'PT0H15M';
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft, userWith({patientListWithNames: true}))).toBe(true);
  });

  it('is complete for a Cerner-shaped draft once tested and acknowledged', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT1H0M';
    draft.census.sftpConnectionTested = true;
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft, userWith({sftpFileListing: true}))).toBe(true);
  });

  it('stays incomplete for a Cerner-shaped draft without a successful connection test, regardless of the flag', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT1H0M';
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft, userWith({sftpFileListing: true}))).toBe(false);
    expect(isComplete(draft, userWith({sftpFileListing: false}))).toBe(false);
  });

  it('stays incomplete if the acknowledged frequency is under 15 minutes', () => {
    const draft = createEmptyDraft();
    draft.census.sftpHost = 'sftp.example.invalid';
    draft.census.sftpPort = 22;
    draft.census.acquisitionFrequency = 'PT0H5M';
    draft.census.sftpConnectionTested = true;
    draft.census.accuracyAcknowledged = true;
    expect(isComplete(draft, userWith({sftpFileListing: true}))).toBe(false);
  });
});
