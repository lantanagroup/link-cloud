import { INotificationModel } from "../../interfaces/notification/notification-model.interface";

export class NotificationModel implements INotificationModel {
    id!: string;
    notificationType!: string;
    facilityId!: string;
    correlationId!: string;
    subject!: string;
    body!: string;
    recipients!: string[];
    bcc!: string[];
    createdOn!: string;
    sentOn: any;
}
