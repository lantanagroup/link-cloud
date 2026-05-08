export interface INotificationModel {
  id: string
  notificationType: string
  facilityId: string
  correlationId: any
  subject: string
  body: string
  recipients: string[]
  bcc: any
  createdOn: string
  sentOn: any
}
