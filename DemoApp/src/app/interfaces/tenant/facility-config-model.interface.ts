export interface IFacilityConfigModel {
  facilityId: string;
  facilityName: string;
  scheduledTasks: IScheduledTaskModel[];
}

export interface IScheduledTaskModel {
  kafkaTopic: string;
  reportTypeSchedules: IReportTypeScheduleModel[];
}

export interface IReportTypeScheduleModel {
  reportType: string;
  scheduledTriggers: string[];
}
