import { PagedUserModel } from '../../src/app/models/user/paged-user-model.model';
import { UserModel } from '../../src/app/models/user/user-model.model';
import { RoleModel } from '../../src/app/models/role/role-model.model';

/**
 * The users grid renders `user.roles` (account-dashboard.component.html:41) but UserModel
 * does not declare the field, so the fixture widens the model instead of dropping it —
 * drift detection stays on everything UserModel does declare, and the gap is recorded here
 * rather than hidden behind an untyped literal.
 */
export type AccountUser = UserModel & { roles: string[] };

/**
 * Roles are opaque names to the UI: the form maps allRoles to `role.name` and posts the
 * names, never the ids (account-config-form.component.ts:117,133). LinkAdministrator is the
 * one the backend actually ships; the second exists only so the multi-select has an option
 * the specs deliberately leave unselected.
 */
export const testRoles: RoleModel[] = [
  {
    id: '37836b46-bf97-47a2-ba6f-3dce45342f7c',
    name: 'LinkAdministrator',
    description: 'Administrator for the Link application',
  },
  {
    id: '9b1c2d34-5e6f-4a7b-8c9d-0e1f2a3b4c5d',
    name: 'LinkReportViewer',
    description: 'Read-only access to reports',
  },
];

export const existingUser: AccountUser = {
  id: '02721a17-b8ea-49aa-9bbc-343e254de581',
  username: 'Ada.Lovelace',
  firstName: 'Ada',
  middleName: '',
  lastName: 'Lovelace',
  email: 'ada.lovelace@example.com',
  roles: [testRoles[0].name],
  isActive: true,
  isDeleted: false,
};

export function pagedUsers(records: AccountUser[] = []): PagedUserModel {
  return {
    records,
    metadata: {
      pageSize: 10,
      // The service decrements pageNumber for zero-based Material paging; serve 1-based.
      pageNumber: 1,
      totalCount: records.length,
      totalPages: records.length === 0 ? 0 : 1,
    },
  };
}
