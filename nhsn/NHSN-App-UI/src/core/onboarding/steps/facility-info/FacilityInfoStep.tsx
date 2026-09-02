import React, {useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {EhrVendor, Timezone, VendorProfile} from '../../../api/contracts';
import {Button, NHSNLoadingIndicator, PageHeader, Select, StepActions} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {validateFacilityInfo} from './validate';

/**
 * Step scaffold. The screen's own fields, validation and API calls are LEGLINK story: facility information.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.*/
export function FacilityInfoStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError} = useNotifications();
  const {draft, patch, saving} = useOnboarding();

  const [loading, setLoading] = useState(true);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [timezones, setTimezones] = useState<Timezone[]>([]);
  const [vendorProfiles, setVendorProfiles] = useState<VendorProfile[]>([]);

  const timeZone = draft.facilityInfo.timeZone ?? '';
  const ehrVendor = draft.facilityInfo.vendor ?? '';

  useEffect(() => {
    let mounted = true;
    setLoading(true);

    Promise.all([api.getTimezones(), api.getVendorProfiles()])
      .then(([zones, profiles]) => {
        if (!mounted) {
          return;
        }
        setTimezones(zones);
        setVendorProfiles(profiles);
      })
      .catch(cause => {
        notifyError(cause instanceof Error ? cause.message : t('onboarding:facilityInfo.messages.loadError'));
      })
      .finally(() => {
        if (mounted) {
          setLoading(false);
        }
      });

    return () => {
      mounted = false;
    };
  }, [api]);

  function handleNext() {
    const errors = validateFacilityInfo(draft);
    if (Object.keys(errors).length > 0) {
      setValidationError(t('onboarding:facilityInfo.messages.incomplete'));
      return;
    }

    setValidationError(null);
    onNext();
  }

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="nhsn-link__content nhsn-facility-info">
      <PageHeader title={t('onboarding:facilityInfo.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:facilityInfo.intro')}</p>

      <div className="nhsn-link__field">
        <Select
          id="facilityTimeZone"
          label={t('onboarding:facilityInfo.fields.timeZoneLabel')}
          hint={t('onboarding:facilityInfo.fields.timeZoneTooltip')}
          placeholder={t('onboarding:facilityInfo.fields.timeZonePlaceholder')}
          required
          value={timeZone}
          options={timezones.map(zone => ({value: zone.id, label: zone.displayName}))}
          popupClassName="nhsn-facility-info-popup"
          onChange={value => patch('facilityInfo', {timeZone: value})} />
      </div>

      <div className="nhsn-link__field">
        <Select
          id="facilityEhrVendor"
          label={t('onboarding:facilityInfo.fields.ehrVendorLabel')}
          placeholder={t('onboarding:facilityInfo.fields.ehrVendorPlaceholder')}
          required
          value={ehrVendor}
          options={vendorProfiles.map(profile => ({value: profile.vendor, label: profile.displayName}))}
          popupClassName="nhsn-facility-info-popup"
          onChange={value => patch('facilityInfo', {vendor: value as EhrVendor})} />
      </div>

      {validationError && (
        <p className="nhsn-link__form-error" role="alert">
          {validationError}
        </p>
      )}

      <StepActions saving={saving}>
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={handleNext} disabled={saving}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default FacilityInfoStep;
