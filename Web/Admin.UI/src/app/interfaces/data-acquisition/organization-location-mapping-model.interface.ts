import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';

// A single facility location mapping row, as returned by the DataAcquisition
// location-mappings endpoint (OrganizationLocationMappingModel). Field casing is camelCase
// to match the service's JSON serialization.
export interface IOrganizationLocationMappingModel {
  locationMappingId: number;
  facilityId: string;
  locationId: string;
  locationName?: string;
  locationAlias?: string;
  partOfValue?: string;
  partOfId?: number | null;
  isOrgLocation: boolean;
  createDate?: string;
  modifiedDate?: string;
  isActive: boolean;
}

// Paged response wrapper for the location-mappings search endpoint
// (PagedConfigModel<OrganizationLocationMappingModel>).
export interface IPagedOrganizationLocationMapping {
  records: IOrganizationLocationMappingModel[];
  metadata: PaginationMetadata;
}
