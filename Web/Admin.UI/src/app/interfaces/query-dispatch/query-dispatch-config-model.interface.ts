export interface IDispatchSchedule {
  event: string;
  duration: string; // ISO-8601 duration (e.g., PT10S)
}

export interface IQueryDispatchConfiguration {
  facilityId: string;
  dispatchSchedules: IDispatchSchedule[];
}
