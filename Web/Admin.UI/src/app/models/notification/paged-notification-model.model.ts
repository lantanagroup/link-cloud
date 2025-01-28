import { PaginationMetadata } from "../pagination-metadata.model";
import { NotificationModel } from "./notification-model.model";

export class PagedNotificationModel {
  records: NotificationModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}
