import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useApiClient } from '../../../api/ApiClientContext';
import type { EhrVendor } from '../../../api/contracts';
import {
  AcronymText,
  acronymTitle,
  Button,
  HeadingPause,
  NHSNLoadingIndicator,
  Select,
  StepActions,
} from '../../../fields';
import { useNotifications } from '../../../notifications/NotificationProvider';
import type { StepProps } from '../../flow';
import { useOnboarding, useStepValidator } from '../../OnboardingProvider';
import { useStableCallback, useStepChrome } from '../../StepChrome';
import type { FacilityInfoDraft } from '../../types';
import { validateFacilityInfo, type FieldErrors } from './validate';

export function FacilityInfoStep({ onNext, onBack }: StepProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const { notifyError } = useNotifications();
  const { draft, patch, saving, savingDirection } = useOnboarding();

  const [validationError, setValidationError] = useState<string | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});

  const timeZone = draft.facilityInfo.timeZone ?? '';
  const ehrVendor = draft.facilityInfo.vendor ?? '';

  const {
    data: timezones = [],
    isLoading: timezonesLoading,
    error: timezonesError
  } = useQuery({
    queryKey: ['timezones'],
    queryFn: () => api.getTimezones(),
    staleTime: Infinity
  });

  const {
    data: vendorProfiles = [],
    isLoading: vendorProfilesLoading,
    error: vendorProfilesError
  } = useQuery({
    queryKey: ['vendorProfiles'],
    queryFn: () => api.getVendorProfiles(),
    staleTime: Infinity
  });

  const loading = timezonesLoading || vendorProfilesLoading;
  const loadError = timezonesError ?? vendorProfilesError;

  useEffect(() => {
    if (loadError) {
      notifyError(
        loadError instanceof Error
          ? loadError.message
          : t('onboarding:facilityInfo.messages.loadError'),
      );
    }
  }, [loadError]);

  function markTouched(field: string) {
    setTouched((prev) => ({ ...prev, [field]: true }));
  }

  function validateStep(): boolean {
    setTouched({ timeZone: true, vendor: true });
    const fieldErrors = validateFacilityInfo(draft);
    setErrors(fieldErrors);
    if (Object.keys(fieldErrors).length > 0) {
      setValidationError(t('onboarding:facilityInfo.messages.incomplete'));
      return false;
    }

    setValidationError(null);
    return true;
  }

  function handleNext() {
    if (!validateStep()) {
      return;
    }
    onNext();
  }

  function refreshErrors(overrides: Partial<FacilityInfoDraft> = {}) {
    const nextErrors = validateFacilityInfo({
      ...draft,
      facilityInfo: { ...draft.facilityInfo, ...overrides },
    });
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length === 0) {
      setValidationError(null);
    }
  }

  useStepValidator(validateStep);

  const stableOnBack = useStableCallback(onBack);
  const stableHandleNext = useStableCallback(handleNext);

  useStepChrome(
    useMemo(
      () =>
        loading
          ? null
          : {
              title: acronymTitle(<HeadingPause>{t('onboarding:facilityInfo.title')}</HeadingPause>),
              footer: (
                <StepActions saving={saving}>
                  <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === 'back'}>
                    {t('common:actions.back')}
                  </Button>
                  <Button onClick={stableHandleNext} disabled={saving} loading={savingDirection === 'next'}>
                    {t('common:actions.continue')}
                  </Button>
                </StepActions>
              )
            },
      [t, loading, saving, savingDirection, stableOnBack, stableHandleNext]
    )
  );

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="nhsn-facility-info">
      <p className="nhsn-link__subtitle">
        {t('onboarding:facilityInfo.intro')}
      </p>

      <div className="nhsn-link__field">
        <Select
          id="facilityTimeZone"
          label={t('onboarding:facilityInfo.fields.timeZoneLabel')}
          hint={t('onboarding:facilityInfo.fields.timeZoneTooltip')}
          placeholder={t('onboarding:facilityInfo.fields.timeZonePlaceholder')}
          required
          error={
            touched.timeZone && errors.timeZone ? t(errors.timeZone) : undefined
          }
          value={timeZone}
          options={timezones.map((zone) => ({
            value: zone.id,
            label: zone.displayName,
          }))}
          popupClassName="nhsn-facility-info-popup"
          onChange={(value) => {
            patch('facilityInfo', { timeZone: value });
            markTouched('timeZone');
            refreshErrors({ timeZone: value });
          }}
          onBlur={() => {
            markTouched('timeZone');
            refreshErrors();
          }}
        />
      </div>

      <div className="nhsn-link__field">
        <Select
          id="facilityEhrVendor"
          label={acronymTitle(<AcronymText>{t('onboarding:facilityInfo.fields.ehrVendorLabel')}</AcronymText>)}
          placeholder={t('onboarding:facilityInfo.fields.ehrVendorPlaceholder')}
          required
          error={touched.vendor && errors.vendor ? t(errors.vendor) : undefined}
          value={ehrVendor}
          options={vendorProfiles.map((profile) => ({
            value: profile.vendor,
            label: profile.displayName,
          }))}
          popupClassName="nhsn-facility-info-popup"
          onChange={(value) => {
            patch('facilityInfo', { vendor: value as EhrVendor });
            markTouched('vendor');
            refreshErrors({ vendor: value as EhrVendor });
          }}
          onBlur={() => {
            markTouched('vendor');
            refreshErrors();
          }}
        />
      </div>

      <div aria-live="off">
        <p className="nhsn-link__form-error" role="alert">
          {validationError}
        </p>
      </div>
    </div>
  );
}

export default FacilityInfoStep;
