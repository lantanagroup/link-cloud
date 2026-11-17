import { IAuditModel } from '../../src/app/interfaces/audit/audit-model.interface';
import { PagedAuditModel } from '../../src/app/models/audit/paged-audit-model.model';

/**
 * Two events sharing a correlation id — the pairing the real log shows, where one service
 * records the change and another records the downstream effect. The second carries no notes
 * so specs can assert that the notes toggle is conditional.
 */
export const auditEvents: IAuditModel[] = [
  {
    id: 'audit-0000-0000-0001',
    facilityId: 'TestFacility01',
    correlationId: '73cea11e-7014-485a-9afa-8d85c2c57b04',
    serviceName: 'Tenant',
    eventDate: '2026-05-13T20:08:00Z',
    user: 'SystemUser',
    action: 'Create',
    resource: 'Facility',
    propertyChanges: [],
    notes: 'Facility TestFacility01 was created.',
  },
  {
    id: 'audit-0000-0000-0002',
    facilityId: 'TestFacility02',
    correlationId: '6f834e77-4db5-4626-bf91-925f40210a47',
    serviceName: 'QueryDispatch',
    eventDate: '2026-05-13T20:09:00Z',
    user: 'SystemUser',
    action: 'Update',
    resource: 'QueryDispatchConfiguration',
    propertyChanges: [],
    notes: '',
  },
];

/**
 * AuditService decrements metadata.pageNumber on the way in (the API is 1-based, the
 * component and Material paginator are 0-based), so fixtures must serve it **1-based**.
 */
export function pagedAuditEvents(
  records: IAuditModel[] = auditEvents,
  totalCount: number = records.length,
): PagedAuditModel {
  return {
    records,
    metadata: {
      pageSize: 10,
      pageNumber: 1,
      totalCount,
      totalPages: Math.max(1, Math.ceil(totalCount / 10)),
    },
  } as PagedAuditModel;
}
