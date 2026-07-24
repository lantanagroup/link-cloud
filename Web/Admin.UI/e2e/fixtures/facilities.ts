import { IFacilityConfigModel, PagedFacilityConfigModel } from '../../src/app/interfaces/tenant/facility-config-model.interface';

export const testFacilities: IFacilityConfigModel[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    facilityId: 'TestFacility01',
    facilityName: 'General Hospital One',
    timeZone: 'America/New_York',
    scheduledReports: { daily: [], weekly: [], monthly: ['NHSNdQMAcuteCareHospitalInitialPopulation'] },
    isDeleted: false,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    facilityId: 'TestFacility02',
    facilityName: 'Community Medical Center',
    timeZone: 'America/Chicago',
    scheduledReports: { daily: [], weekly: [], monthly: [] },
    isDeleted: false,
  },
];

export function pagedFacilities(records: IFacilityConfigModel[] = testFacilities): PagedFacilityConfigModel {
  return {
    records,
    metadata: {
      pageSize: 10,
      // The service decrements pageNumber for zero-based Material paging; serve 1-based.
      pageNumber: 1,
      totalCount: records.length,
      totalPages: Math.max(1, Math.ceil(records.length / 10)),
    },
  };
}

/** Shape of GET /api/facility/list — { [facilityId]: facilityName }. */
export function facilityLookup(records: IFacilityConfigModel[] = testFacilities): Record<string, string> {
  return Object.fromEntries(records.map((f) => [f.facilityId, f.facilityName]));
}
