import React, {ChangeEvent} from 'react';
import {TestUserProfile, UserRoleSummaryResponse} from '../shared/models';
import {UserInfoService} from '../services/user-info-service';
import {useNotifications} from './notifications/NotificationProvider';

interface SystemAdminUsersScreenProps {
  activeTestUser: TestUserProfile;
  userInfoService: UserInfoService;
  users: UserRoleSummaryResponse[];
  usersError: string | null;
  savingUserId: string | null;
  onUsersChanged: React.Dispatch<React.SetStateAction<UserRoleSummaryResponse[]>>;
  onUsersErrorChanged: React.Dispatch<React.SetStateAction<string | null>>;
  onSavingUserIdChanged: React.Dispatch<React.SetStateAction<string | null>>;
}

const availableRoles = ['System Admin', 'Facility Admin', 'Facility IT'];

export function SystemAdminUsersScreen({
  activeTestUser,
  userInfoService,
  users,
  usersError,
  savingUserId,
  onUsersChanged,
  onUsersErrorChanged,
  onSavingUserIdChanged
}: SystemAdminUsersScreenProps) {
  const { notifySuccess, notifyError } = useNotifications();

  async function handleRoleChanged(user: UserRoleSummaryResponse, event: ChangeEvent<HTMLSelectElement>) {
    const selectedRole = event.target.value;
    const nextRoles = selectedRole ? [selectedRole] : [];

    try {
      onSavingUserIdChanged(user.Id);
      onUsersErrorChanged(null);
      const updated = await userInfoService.updateUserRoles(activeTestUser, user.Id, nextRoles);
      onUsersChanged(current => current.map(existing => existing.Id === updated.Id ? updated : existing));
      notifySuccess(`Updated roles for ${updated.Name}.`);
    } catch (updateError) {
      const message = updateError instanceof Error ? updateError.message : 'Unable to update roles.';
      onUsersErrorChanged(message);
      notifyError(message);
    } finally {
      onSavingUserIdChanged(null);
    }
  }

  return (
    <div className="nhsn-link__content">
      <h2>User administration</h2>
      <p>
        System administrators have a separate experience from facility users. The initial framework screen allows them to review users and change NHSNLink roles.
      </p>
      {usersError && <p>{usersError}</p>}
      <div>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
          <tr>
            <th align="left">User</th>
            <th align="left">Facility</th>
            <th align="left">Roles</th>
          </tr>
          </thead>
          <tbody>
          {users.map(user => (
            <tr key={user.Id}>
              <td style={{ padding: '0.5rem 0' }}>
                <div><strong>{user.Name}</strong></div>
                <div>{user.Email}</div>
              </td>
              <td>{user.FacilityId ?? 'Not assigned'}</td>
              <td>
                <select
                  value={user.Roles[0] ?? ''}
                  disabled={savingUserId === user.Id}
                  onChange={event => handleRoleChanged(user, event)}>
                  <option value="">No role assigned</option>
                  {availableRoles.map(role => (
                    <option key={role} value={role}>{role}</option>
                  ))}
                </select>
                {user.Roles.length > 1 && (
                  <div style={{ marginTop: '0.35rem', fontSize: '0.9rem' }}>
                    Additional roles currently assigned: {user.Roles.slice(1).join(', ')}
                  </div>
                )}
              </td>
            </tr>
          ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}