/**
 * The lifecycle status of a report schedule, as the Report service serializes it.
 *
 * This is the internal state machine, not the facility-facing status. The two are
 * deliberately different vocabularies: the facing projection collapses these onto
 * Pending, Completed and Canceled.
 */
export enum ScheduleStatus {
  New = 'New',
  Scheduled = 'Scheduled',
  EndOfPeriod = 'EndOfPeriod',
  Submitted = 'Submitted',
  /**
   * Terminal, like Submitted: the report finished all of its work but was requested with
   * bypassSubmission, so nothing was published to the external container.
   */
  CompletedNotSubmitted = 'CompletedNotSubmitted'
}

export interface ScheduleStatusDisplay {
  label: string;
  class: string;
}

/**
 * How each status is shown: the text, and the badge class the stylesheets define.
 *
 * Typed as a complete Record so adding a ScheduleStatus member fails the build until it is
 * given both, rather than silently rendering its raw value in every grid.
 */
const SCHEDULE_STATUS_DISPLAY: Record<ScheduleStatus, ScheduleStatusDisplay> = {
  [ScheduleStatus.New]: { label: 'New', class: 'schedule-status-new' },
  [ScheduleStatus.Scheduled]: { label: 'Scheduled', class: 'schedule-status-scheduled' },
  [ScheduleStatus.EndOfPeriod]: { label: 'End of Period', class: 'schedule-status-endOfPeriod' },
  [ScheduleStatus.Submitted]: { label: 'Submitted', class: 'schedule-status-submitted' },
  [ScheduleStatus.CompletedNotSubmitted]: {
    label: 'Completed (Submission Skipped)',
    class: 'schedule-status-completedNotSubmitted'
  }
};

/** Status filter options, in declaration order. */
export const SCHEDULE_STATUS_OPTIONS: string[] = Object.keys(SCHEDULE_STATUS_DISPLAY);

/**
 * Display text for a status. Takes a string rather than the enum because the API types the
 * field as a plain string; an unrecognised value falls back to itself so a grid cell shows
 * something rather than nothing.
 */
export function scheduleStatusLabel(status: string): string {
  return SCHEDULE_STATUS_DISPLAY[status as ScheduleStatus]?.label ?? status;
}

/** Badge class for a status, or none when it is unrecognised. */
export function scheduleStatusBadgeClass(status: string): string {
  return SCHEDULE_STATUS_DISPLAY[status as ScheduleStatus]?.class ?? '';
}
