import { ICensusConfiguration } from '../../src/app/interfaces/census/census-config-model.interface';
import { IQueryDispatchConfiguration } from '../../src/app/interfaces/query-dispatch/query-dispatch-config-model.interface';
import {
  IEncounterMappingModel,
  IPagedEncounterMapping,
} from '../../src/app/interfaces/data-acquisition/encounter-mapping-model.interface';
import {
  IOrganizationLocationMappingModel,
  IPagedOrganizationLocationMapping,
} from '../../src/app/interfaces/data-acquisition/organization-location-mapping-model.interface';
import { IOperationModel, IPagedOperationModel } from '../../src/app/interfaces/normalization/operation-get-model.interface';
import { testFacilities } from './facilities';

const facilityId = testFacilities[0].facilityId;

export const locationMappings: IOrganizationLocationMappingModel[] = [
  {
    locationMappingId: 1,
    facilityId,
    locationId: 'LOC-001',
    locationName: 'Medical Intensive Care Unit',
    locationAlias: 'MICU',
    partOfValue: 'General Hospital One',
    partOfId: null,
    isOrgLocation: true,
    createDate: '2026-06-01T00:00:00Z',
    isActive: true,
  },
  {
    locationMappingId: 2,
    facilityId,
    locationId: 'LOC-002',
    locationName: 'Surgical Ward',
    locationAlias: 'SURG',
    partOfValue: 'General Hospital One',
    partOfId: 1,
    isOrgLocation: false,
    createDate: '2026-06-02T00:00:00Z',
    isActive: false,
  },
];

/** The service decrements metadata.pageNumber for zero-based Material paging; serve 1-based. */
export function pagedLocationMappings(
  records: IOrganizationLocationMappingModel[] = locationMappings,
): IPagedOrganizationLocationMapping {
  return {
    records,
    metadata: { pageSize: 10, pageNumber: 1, totalCount: records.length, totalPages: Math.max(1, Math.ceil(records.length / 10)) },
  };
}

export const encounterMappings: IEncounterMappingModel[] = [
  {
    encounterMappingId: 1,
    facilityId,
    patientId: 'PAT-1001',
    encounterId: 'ENC-5001',
    mappedToOrg: true,
    createDate: '2026-06-03T00:00:00Z',
    encounterLocations: [
      { encounterLocationId: 1, encounterMappingId: 1, organizationLocationMappingId: 1, locationId: 'LOC-001' },
    ],
  },
  {
    encounterMappingId: 2,
    facilityId,
    patientId: 'PAT-1002',
    encounterId: 'ENC-5002',
    mappedToOrg: false,
    createDate: '2026-06-04T00:00:00Z',
    encounterLocations: [],
  },
];

export function pagedEncounterMappings(
  records: IEncounterMappingModel[] = encounterMappings,
): IPagedEncounterMapping {
  return {
    records,
    metadata: { pageSize: 10, pageNumber: 1, totalCount: records.length, totalPages: Math.max(1, Math.ceil(records.length / 10)) },
  };
}

/**
 * The Normalization panel's operations list is not behind an expansion lazy-load — it renders
 * with the edit page — so every facility-edit spec has to answer this endpoint.
 */
export function pagedOperations(records: IOperationModel[] = []): IPagedOperationModel {
  return {
    records,
    metadata: { pageSize: 10, pageNumber: 1, totalCount: records.length, totalPages: Math.max(1, Math.ceil(records.length / 10)) },
  };
}

export const censusConfiguration: ICensusConfiguration = {
  facilityId,
  scheduledTrigger: '0 0 6 * * ?',
  enabled: true,
};

/**
 * Discharge is the only event the form offers — its control is rendered disabled — so a
 * configured facility always comes back with exactly this one schedule.
 */
export const queryDispatchConfiguration: IQueryDispatchConfiguration = {
  facilityId,
  dispatchSchedules: [{ event: 'Discharge', duration: 'PT30M' }],
};
