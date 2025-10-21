import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';

// A single location associated with an encounter mapping. LocationId is the human-facing
// identifier resolved from the underlying OrganizationLocationMapping.
export interface IEncounterLocationModel {
  encounterLocationId: number;
  encounterMappingId: number;
  organizationLocationMappingId: number;
  locationId?: string | null;
  createDate?: string;
  modifiedDate?: string;
}

// A single encounter mapping row, as returned by the DataAcquisition
// encounter-mappings search endpoint (EncounterMappingModel). Field casing is camelCase
// to match the service's JSON serialization.
export interface IEncounterMappingModel {
  encounterMappingId: number;
  facilityId: string;
  patientId: string;
  encounterId: string;
  mappedToOrg: boolean;
  createDate?: string;
  modifiedDate?: string;
  encounterLocations: IEncounterLocationModel[];
}

// Paged response wrapper for the encounter-mappings search endpoint
// (PagedConfigModel<EncounterMappingModel>).
export interface IPagedEncounterMapping {
  records: IEncounterMappingModel[];
  metadata: PaginationMetadata;
}
