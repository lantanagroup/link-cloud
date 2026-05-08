import { IPaginationMetadata } from "../interfaces/pagination-metadata.interface"

export class PaginationMetadata implements IPaginationMetadata {
  pageSize!: number
  pageNumber!: number
  totalCount!: number
  totalPages!: number
}
