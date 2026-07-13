import React from 'react';
import {TestUserProfile, UserRoleSummaryResponse} from '../shared/models';
import {UserInfoService} from '../services/user-info-service';
import {useNotifications} from './notifications/NotificationProvider';

interface SystemAdminUsersScreenProps {
  activeTestUser: TestUserProfile;
  currentUserEmail: string;
  userInfoService: UserInfoService;
  users: UserRoleSummaryResponse[];
  usersError: string | null;
  savingUserId: string | null;
  onUsersChanged: React.Dispatch<React.SetStateAction<UserRoleSummaryResponse[]>>;
  onUsersErrorChanged: React.Dispatch<React.SetStateAction<string | null>>;
  onSavingUserIdChanged: React.Dispatch<React.SetStateAction<string | null>>;
}

export function SystemAdminUsersScreen({
  activeTestUser,
  currentUserEmail,
  userInfoService,
  users,
  usersError,
  savingUserId,
  onUsersChanged,
  onUsersErrorChanged,
  onSavingUserIdChanged
}: SystemAdminUsersScreenProps) {
  const { notifySuccess, notifyError } = useNotifications();
  const normalizedCurrentUserEmail = currentUserEmail.toLowerCase();

  async function handleAdminToggle(user: UserRoleSummaryResponse, nextIsAdmin: boolean) {
    try {
      onSavingUserIdChanged(user.Id);
      onUsersErrorChanged(null);
      const updated = await userInfoService.updateUserAdmin(activeTestUser, user.Id, nextIsAdmin);
      onUsersChanged(current => current.map(existing => existing.Id === updated.Id ? updated : existing));
      notifySuccess(nextIsAdmin
        ? `Granted NHSNLINKSYSADMIN to ${updated.Name}.`
        : `Removed NHSNLINKSYSADMIN from ${updated.Name}.`);
    } catch (updateError) {
      const message = updateError instanceof Error ? updateError.message : 'Unable to update admin flag.';
      onUsersErrorChanged(message);
      notifyError(message);
    } finally {
      onSavingUserIdChanged(null);
    }
  }

  async function handleStatusToggle(user: UserRoleSummaryResponse, nextIsActive: boolean) {
    const confirmed = window.confirm(nextIsActive
      ? `Restore access for ${user.Name}?`
      : `Disable access for ${user.Name}?`);

    if (!confirmed) {
      return;
    }

    try {
      onSavingUserIdChanged(user.Id);
      onUsersErrorChanged(null);
      const updated = await userInfoService.updateUserStatus(activeTestUser, user.Id, nextIsActive);
      onUsersChanged(current => current.map(existing => existing.Id === updated.Id ? updated : existing));
      notifySuccess(nextIsActive
        ? `Restored NHSNLink access for ${updated.Name}.`
        : `Disabled NHSNLink access for ${updated.Name}.`);
    } catch (updateError) {
      const message = updateError instanceof Error ? updateError.message : 'Unable to update user status.';
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
            <th align="left">NHSNLink Sys Admin</th>
            <th align="left">Access</th>
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
                <label>
                  <input
                    type="checkbox"
                    checked={user.IsAdmin}
                    disabled={savingUserId === user.Id || user.Email.toLowerCase() === normalizedCurrentUserEmail}
                    onChange={event => handleAdminToggle(user, event.target.checked)} /> NHSNLINKSYSADMIN
                </label>
                {user.Email.toLowerCase() === normalizedCurrentUserEmail && (
                  <div style={{ marginTop: '0.35rem', fontSize: '0.9rem' }}>
                    You cannot change your own admin flag.
                  </div>
                )}
                {user.Groups.length > 0 && (
                  <div style={{ marginTop: '0.35rem', fontSize: '0.9rem' }}>
                    Incoming JWT groups: {user.Groups.join(', ')}
                  </div>
                )}
              </td>
              <td>
                <button
                  type="button"
                  disabled={savingUserId === user.Id || user.Email.toLowerCase() === normalizedCurrentUserEmail}
                  onClick={() => handleStatusToggle(user, !user.IsActive)}>
                  {user.IsActive ? 'Disable user' : 'Enable user'}
                </button>
                {user.Email.toLowerCase() === normalizedCurrentUserEmail && (
                  <div style={{ marginTop: '0.35rem', fontSize: '0.9rem' }}>
                    You cannot disable your own account.
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