import { IPagedReportSchedule, IReportSchedule } from '../../src/app/interfaces/report/report-schedule.interface';

export const reportSchedules: IReportSchedule[] = [
  {
    id: 'aaaaaaaa-0000-0000-0000-000000000001',
    createDate: new Date('2026-06-01T00:00:00Z'),
    facilityId: 'TestFacility01',
    reportStartDate: new Date('2026-05-01T00:00:00Z'),
    reportEndDate: new Date('2026-05-31T23:59:59Z'),
    enableSubmission: true,
    endOfReportPeriodJobHasRun: true,
    reportTypes: ['NHSNdQMAcuteCareHospitalInitialPopulation'],
    frequency: 'Monthly',
    status: 'Submitted',
    censusCount: 120,
    initialPopulationCount: 45,
  },
  {
    id: 'aaaaaaaa-0000-0000-0000-000000000002',
    createDate: new Date('2026-07-01T00:00:00Z'),
    facilityId: 'TestFacility02',
    reportStartDate: new Date('2026-06-01T00:00:00Z'),
    reportEndDate: new Date('2026-06-30T23:59:59Z'),
    enableSubmission: false,
    endOfReportPeriodJobHasRun: false,
    reportTypes: ['NHSNGlycemicControlHypoglycemicInitialPopulation'],
    frequency: 'Monthly',
    status: 'InProgress',
    censusCount: 87,
    initialPopulationCount: 12,
  },
];

export function pagedReportSchedules(records: IReportSchedule[] = reportSchedules): IPagedReportSchedule {
  return {
    records,
    metadata: { pageSize: 10, pageNumber: 1, totalCount: records.length, totalPages: 1 },
  };
}
