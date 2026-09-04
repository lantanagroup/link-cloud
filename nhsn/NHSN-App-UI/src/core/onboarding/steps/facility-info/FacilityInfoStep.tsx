import React, {useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {EhrVendor, Timezone, VendorProfile} from '../../../api/contracts';
import {Button, NHSNLoadingIndicator, PageHeader, Select, StepActions} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {validateFacilityInfo, type FieldErrors} from './validate';

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
  const [errors, setErrors] = useState<FieldErrors>({});
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
    const fieldErrors = validateFacilityInfo(draft);
    setErrors(fieldErrors);
    if (Object.keys(fieldErrors).length > 0) {
      setValidationError(t('onboarding:facilityInfo.messages.incomplete'));
      return;
    }

    setValidationError(null);
    onNext();
  }

  function refreshFieldError(field: string) {
    const nextErrors = validateFacilityInfo(draft);
    setErrors(prev => {
      const next = {...prev};
      if (nextErrors[field]) {
        next[field] = nextErrors[field];
      } else {
        delete next[field];
      }
      return next;
    });
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
          error={errors.timeZone ? t(errors.timeZone) : undefined}
          value={timeZone}
          options={timezones.map(zone => ({value: zone.id, label: zone.displayName}))}
          popupClassName="nhsn-facility-info-popup"
          onChange={value => patch('facilityInfo', {timeZone: value})}
          onBlur={() => refreshFieldError('timeZone')} />
      </div>

      <div className="nhsn-link__field">
        <Select
          id="facilityEhrVendor"
          label={t('onboarding:facilityInfo.fields.ehrVendorLabel')}
          placeholder={t('onboarding:facilityInfo.fields.ehrVendorPlaceholder')}
          required
          error={errors.vendor ? t(errors.vendor) : undefined}
          value={ehrVendor}
          options={vendorProfiles.map(profile => ({value: profile.vendor, label: profile.displayName}))}
          popupClassName="nhsn-facility-info-popup"
          onChange={value => patch('facilityInfo', {vendor: value as EhrVendor})}
          onBlur={() => refreshFieldError('vendor')} />
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
        <Button onClick={handleNext} disabled={saving} loading={saving}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default FacilityInfoStep;
