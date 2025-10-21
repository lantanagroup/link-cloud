import { Frequency } from './measure-mapping.interface';

/**
 * A facility's enrollment in one NHSN measure for one reporting period, as recorded from DMRP.
 * Matches FacilityReportingPlanModel: measure, dqm and frequency come resolved from the plan's
 * measure mapping on the per-facility read, and rows with isReporting false are kept as the
 * record of a withdrawn enrollment rather than deleted.
 */
export interface IFacilityReportingPlan {
  id: string;
  facilityId: string;
  measureMappingId: string;
  reportingMonth: number;
  reportingYear: number;
  isReporting: boolean;
  measure: string | null;
  dqm: string | null;
  frequency: Frequency | null;
  createDate: string;
  modifyDate: string | null;
}
