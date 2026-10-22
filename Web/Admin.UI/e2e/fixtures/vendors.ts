import { IVendor, IVendorVersion } from '../../src/app/interfaces/tenant/vendor-interface';

/**
 * GET /api/vendor — the tenant dashboard's Vendor filter.
 *
 * A lookup list the facility screens load on open rather than anything a screen's spec is
 * about, so it is registered as a default in support/test.ts. A spec that asserts on the
 * vendor list itself should mock it explicitly instead of leaning on that default.
 */
export const vendors: IVendor[] = [
  { id: 'epic-id', name: 'Epic' },
  { id: 'cerner-id', name: 'Cerner' },
];

/** GET /api/VendorVersion — the Vendor select in the facility configuration form. */
export const vendorVersions: IVendorVersion[] = [
  { id: 'epic-v1', vendorId: 'epic-id', vendorName: 'Epic', version: '1.0' },
  { id: 'cerner-v1', vendorId: 'cerner-id', vendorName: 'Cerner', version: '1.0' },
];
