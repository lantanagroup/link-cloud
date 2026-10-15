/**
 * Tenant's answer at api/dmrp/dmrp-status: whether DMRP is enabled, and whether facility saves write
 * through to the Mock DMRP API.
 */
export interface IDmrpStatus {
  dmrpEnabled: boolean;
  mockDmrpEnabled: boolean;
}
