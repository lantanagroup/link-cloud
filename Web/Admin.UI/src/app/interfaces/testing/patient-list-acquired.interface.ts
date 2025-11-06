export interface IPatientListAcquired {
  facilityId: string;
  patientLists: Array<{
    listType: "Admit" | "Discharge";
    timeFrame: "LessThan24Hours" | "Between24To48Hours" | "MoreThan48Hours";
    patientIds: string[];
  }>;
  reportTrackingId: string;
}
