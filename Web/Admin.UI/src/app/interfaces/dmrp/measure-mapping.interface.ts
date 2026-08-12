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
