import { IPaginationMetadata } from "../pagination-metadata.interface";

// Matches DotNet/Shared/Application/Models/Frequency.cs, which serializes by member name
// (JsonStringEnumConverter), not by its [StringValue] display text.
export enum Frequency {
  Discharge = 'Discharge',
  Daily = 'Daily',
  Weekly = 'Weekly',
  Monthly = 'Monthly',
  Adhoc = 'Adhoc'
}

// DMRP reporting plans run on a calendar cadence, so measure mappings only accept these
// three; the other Frequency members exist for the rest of the enum's consumers.
export const MEASURE_MAPPING_FREQUENCIES: readonly Frequency[] = [
  Frequency.Daily,
  Frequency.Weekly,
  Frequency.Monthly
];

export interface IMeasureMapping {
  id: string;
  measure: string;
  dqm: string;
  frequency: Frequency;
}

export interface IPagedMeasureMapping {
  records: IMeasureMapping[];
  metadata: IPaginationMetadata;
}
