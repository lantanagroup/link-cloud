import { IDmrpStatus } from '../../src/app/interfaces/dmrp/dmrp-status.interface';

/**
 * The nav bar asks Tenant for this on every page, so it is a shell call rather than anything
 * an individual screen's spec is about. Off is also what the app assumes when Tenant cannot
 * be reached (DMRP_OFF in dmrp-status.service.ts), so it keeps the mocked tier on the same
 * path the screens were written against. A spec that needs the module on overrides it with
 * api.mock('GET /api/dmrp/dmrp-status', { dmrpEnabled: true, mockDmrpEnabled: false }).
 */
export const dmrpOff: IDmrpStatus = { dmrpEnabled: false, mockDmrpEnabled: false };
