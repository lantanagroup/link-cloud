export interface INotificationMessage {
  notificationType: string
  facilityId: string
  correlationId: any
  subject: string
  body: string
  recipients: string[]
  bcc: string[]
}
