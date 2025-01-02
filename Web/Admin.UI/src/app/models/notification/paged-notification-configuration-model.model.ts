import { INotificationConfiguration } from "../../interfaces/notification/notification-configuration-model.interface";
import { PaginationMetadata } from "../pagination-metadata.model";

export class PagedNotificationConfigurationModel {
  records: INotificationConfiguration[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}
