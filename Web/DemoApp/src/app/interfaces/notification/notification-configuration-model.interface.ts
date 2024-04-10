export interface INotificationConfiguration {
  id: string;
  facilityId: string;
  emailAddresses: string[];
  channels: IFacilityChannel[];
}

export interface IFacilityChannel {
  name: string;
  enabled: boolean;
}
