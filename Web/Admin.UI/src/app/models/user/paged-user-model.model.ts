import { PaginationMetadata } from "../pagination-metadata.model";
import { UserModel } from "./user-model.model";

export class PagedUserModel {
  records: UserModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}
