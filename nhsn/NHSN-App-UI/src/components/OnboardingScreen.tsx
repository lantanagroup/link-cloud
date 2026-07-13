import React from 'react';
import {TestUserProfile} from '../shared/models';
import {UserInfoService} from '../services/user-info-service';
import {useNotifications} from './notifications/NotificationProvider';

interface OnboardingScreenProps {
  activeTestUser: TestUserProfile;
  facilityId: string;
  userInfoService: UserInfoService;
  onCompleted: () => void;
}

export function OnboardingScreen({ activeTestUser, facilityId, userInfoService, onCompleted }: OnboardingScreenProps) {
  const { notifySuccess, notifyError } = useNotifications();

  async function completeOnboarding() {
    try {
      await userInfoService.updateFacilityOnboarding(activeTestUser, facilityId, true);
      notifySuccess(`Completed onboarding for facility ${facilityId}.`);
      onCompleted();
    } catch (error) {
      notifyError(error instanceof Error ? error.message : 'Unable to complete onboarding.');
    }
  }

  return (
    <div className="nhsn-link__content">
      <h2>Onboarding</h2>
      <p>Hello world...</p>
      <button type="button" className="nhsn-link__action-button" onClick={completeOnboarding}>
        Complete Onboarding
      </button>
    </div>
  );
}