import React from 'react';
import {FacilitySummaryResponse, TestUserProfile} from '../shared/models';
import {UserInfoService} from '../services/user-info-service';
import {useNotifications} from './notifications/NotificationProvider';

interface SystemAdminFacilitiesScreenProps {
  activeTestUser: TestUserProfile;
  facilities: FacilitySummaryResponse[];
  facilitiesError: string | null;
  savingFacilityId: string | null;
  userInfoService: UserInfoService;
  onFacilitiesChanged: React.Dispatch<React.SetStateAction<FacilitySummaryResponse[]>>;
  onFacilitiesErrorChanged: React.Dispatch<React.SetStateAction<string | null>>;
  onSavingFacilityIdChanged: React.Dispatch<React.SetStateAction<string | null>>;
}

export function SystemAdminFacilitiesScreen({
  activeTestUser,
  facilities,
  facilitiesError,
  savingFacilityId,
  userInfoService,
  onFacilitiesChanged,
  onFacilitiesErrorChanged,
  onSavingFacilityIdChanged
}: SystemAdminFacilitiesScreenProps) {
  const { notifySuccess, notifyError } = useNotifications();

  async function handleOnboardingToggle(facility: FacilitySummaryResponse, nextIsOnboarded: boolean) {
    try {
      onSavingFacilityIdChanged(facility.FacilityId);
      onFacilitiesErrorChanged(null);
      const updated = await userInfoService.updateFacilityOnboarding(activeTestUser, facility.FacilityId, nextIsOnboarded);
      onFacilitiesChanged(current => current.map(existing => existing.FacilityId === updated.FacilityId ? updated : existing));
      notifySuccess(nextIsOnboarded
        ? `Marked facility ${updated.FacilityId} as onboarded.`
        : `Marked facility ${updated.FacilityId} as not onboarded.`);
    } catch (updateError) {
      const message = updateError instanceof Error ? updateError.message : 'Unable to update facility onboarding.';
      onFacilitiesErrorChanged(message);
      notifyError(message);
    } finally {
      onSavingFacilityIdChanged(null);
    }
  }

  return (
    <div className="nhsn-link__content">
      <h2>Facilities</h2>
      <p>Facilities that have started onboarding can be reviewed here, including the ability to change their onboarded flag.</p>
      {facilitiesError && <p>{facilitiesError}</p>}
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
        <tr>
          <th align="left">Facility</th>
          <th align="left">Onboarded</th>
        </tr>
        </thead>
        <tbody>
        {facilities.map(facility => (
          <tr key={facility.Id}>
            <td style={{ padding: '0.5rem 0' }}>{facility.FacilityId}</td>
            <td>
              <label>
                <input
                  type="checkbox"
                  checked={facility.IsOnboarded}
                  disabled={savingFacilityId === facility.FacilityId}
                  onChange={event => handleOnboardingToggle(facility, event.target.checked)} /> Onboarded
              </label>
            </td>
          </tr>
        ))}
        </tbody>
      </table>
    </div>
  );
}