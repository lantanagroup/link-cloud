import React from 'react';
import {useTranslation} from 'react-i18next';
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
  const {t} = useTranslation(['onboarding', 'common']);

  async function completeOnboarding() {
    try {
      await userInfoService.updateFacilityOnboarding(activeTestUser, facilityId, true);
      notifySuccess(t('onboarding:messages.completeSuccess', { facilityId }));
      onCompleted();
    } catch (error) {
      notifyError(error instanceof Error ? error.message : t('onboarding:messages.completeError'));
    }
  }

  return (
    <div className="nhsn-link__content">
      <h2>{t('onboarding:page.title')}</h2>
      <p>{t('onboarding:page.intro')}</p>
      <button type="button" className="nhsn-link__action-button" onClick={completeOnboarding}>
        {t('common:actions.completeOnboarding')}
      </button>
    </div>
  );
}