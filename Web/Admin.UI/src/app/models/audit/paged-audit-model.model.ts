import { PaginationMetadata } from "../pagination-metadata.model";
import { AuditModel } from "./audit-model.model";

export class PagedAuditModel {
  records: AuditModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata; 
}
