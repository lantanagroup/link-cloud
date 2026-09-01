import React, {useEffect, useRef, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import {Button, NHSNLoadingIndicator, NumberField, PageHeader, StepActions, TextField} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {isValidHttpUrl, validateFhir, type FhirFieldValues} from './validate';
import './FhirStep.css';

/**
 * Step scaffold. The screen's own fields, validation and API calls are LEGLINK story: FHIR server information.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */
export function FhirStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError} = useNotifications();
  const {patch, saving, vendorProfile} = useOnboarding();

  const [loading, setLoading] = useState(true);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [baseUrlError, setBaseUrlError] = useState<string | null>(null);
  const [maxConcurrentRequestsError, setMaxConcurrentRequestsError] = useState<string | null>(null);
  const [maxRetriesError, setMaxRetriesError] = useState<string | null>(null);
  const [minPullTimeError, setMinPullTimeError] = useState<string | null>(null);
  const [maxPullTimeError, setMaxPullTimeError] = useState<string | null>(null);
  const [lagDaysError, setLagDaysError] = useState<string | null>(null);
  const [lagHoursError, setLagHoursError] = useState<string | null>(null);
  const [lagMinutesError, setLagMinutesError] = useState<string | null>(null);

  const [baseUrl, setBaseUrl] = useState('');
  const [maxConcurrentRequests, setMaxConcurrentRequests] = useState<number | undefined>(undefined);
  const [maxRetries, setMaxRetries] = useState<number | undefined>(undefined);
  const [minPullTime, setMinPullTime] = useState('');
  const [maxPullTime, setMaxPullTime] = useState('');
  const [lagDays, setLagDays] = useState<number | undefined>(undefined);
  const [lagHours, setLagHours] = useState<number | undefined>(undefined);
  const [lagMinutes, setLagMinutes] = useState<number | undefined>(undefined);

  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{success: boolean; message: string} | null>(null);
  const [testedBaseUrl, setTestedBaseUrl] = useState<string | null>(null);
  const cardScrollRef = useRef<HTMLDivElement | null>(null);

  const [readyToAdvance, setReadyToAdvance] = useState(false);

  useEffect(() => {
    if (readyToAdvance) {
      setReadyToAdvance(false);
      onNext();
    }
  }, [readyToAdvance, onNext]);

  useEffect(() => {
    if (testing || testResult) {
      const container = cardScrollRef.current;
      if (container) {
        container.scrollTo({top: container.scrollHeight, behavior: 'smooth'});
      }
    }
  }, [testing, testResult]);

  useEffect(() => {
    let mounted = true;
    setLoading(true);

    api
      .getFhirServerInfo()
      .then(info => {
        if (!mounted) {
          return;
        }
        setBaseUrl(info.fhirServerBaseUrl ?? '');
        setMaxConcurrentRequests(info.maxConcurrentRequests ?? undefined);
        setMaxRetries(info.maxRetries ?? undefined);
        setMinPullTime(info.minAcquisitionPullTime ?? '');
        setMaxPullTime(info.maxAcquisitionPullTime ?? '');
        setLagDays(info.lagDays ?? undefined);
        setLagHours(info.lagHours ?? undefined);
        setLagMinutes(info.lagMinutes ?? undefined);
      })
      .catch(cause => {
        notifyError(cause instanceof Error ? cause.message : t('onboarding:fhirServerInfo.messages.loadError'));
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

  function handleBaseUrlChange(value: string) {
    setBaseUrl(value);
    setTestedBaseUrl(null);
  }

  function handleBaseUrlBlur() {
    const trimmed = baseUrl.trim();
    setBaseUrlError(trimmed && !isValidHttpUrl(trimmed) ? t('onboarding:fhirServerInfo.messages.invalidBaseUrl') : null);
  }

  function currentFieldValues(overrides: Partial<FhirFieldValues> = {}): FhirFieldValues {
    return {
      fhirServerBaseUrl: baseUrl,
      maxConcurrentRequests,
      maxRetries,
      minAcquisitionPullTime: minPullTime,
      maxAcquisitionPullTime: maxPullTime,
      lagDays,
      lagHours,
      lagMinutes,
      ...overrides
    };
  }

  function validateField(field: keyof FhirFieldValues, setError: (message: string | null) => void, overrides?: Partial<FhirFieldValues>) {
    const errors = validateFhir(currentFieldValues(overrides));
    setError(errors[field] ? t(errors[field]) : null);
  }

  function handlePullTimeBlur(value: string, setter: (value: string) => void, field: 'minAcquisitionPullTime' | 'maxAcquisitionPullTime', setError: (message: string | null) => void) {
    const normalized = normalizePullTime(value);
    setter(normalized);
    validateField(field, setError, {[field]: normalized});
  }

  async function handleTestConnection() {
    setTesting(true);
    setTestResult(null);

    const trimmedBaseUrl = baseUrl.trim();

    try {
      const result = await api.testFhirConnection({
        fhirServerBaseUrl: trimmedBaseUrl,
        maxConcurrentRequests,
        maxRetries,
        minAcquisitionPullTime: minPullTime,
        maxAcquisitionPullTime: maxPullTime,
        lagDuration: buildIso8601Duration(lagDays, lagHours, lagMinutes)
      });
      setTestResult({
        success: result.success,
        message: t(result.success ? 'onboarding:fhirServerInfo.messages.testSuccess' : 'onboarding:fhirServerInfo.messages.testFailure')
      });
      setTestedBaseUrl(result.success ? trimmedBaseUrl : null);
    } catch (cause) {
      setTestResult({
        success: false,
        message: cause instanceof Error ? cause.message : t('onboarding:fhirServerInfo.messages.testError')
      });
      setTestedBaseUrl(null);
    } finally {
      setTesting(false);
    }
  }

  function handleNext() {
    const trimmedBaseUrl = baseUrl.trim();
    const errors = validateFhir({
      fhirServerBaseUrl: baseUrl,
      maxConcurrentRequests,
      maxRetries,
      minAcquisitionPullTime: minPullTime,
      maxAcquisitionPullTime: maxPullTime,
      lagDays,
      lagHours,
      lagMinutes
    });

    setBaseUrlError(errors.fhirServerBaseUrl ? t(errors.fhirServerBaseUrl) : null);
    setMaxConcurrentRequestsError(errors.maxConcurrentRequests ? t(errors.maxConcurrentRequests) : null);
    setMaxRetriesError(errors.maxRetries ? t(errors.maxRetries) : null);
    setMinPullTimeError(errors.minAcquisitionPullTime ? t(errors.minAcquisitionPullTime) : null);
    setMaxPullTimeError(errors.maxAcquisitionPullTime ? t(errors.maxAcquisitionPullTime) : null);
    setLagDaysError(errors.lagDays ? t(errors.lagDays) : null);
    setLagHoursError(errors.lagHours ? t(errors.lagHours) : null);
    setLagMinutesError(errors.lagMinutes ? t(errors.lagMinutes) : null);

    if (Object.keys(errors).length > 0) {
      setValidationError(t('onboarding:fhirServerInfo.messages.incomplete'));
      return;
    }

    if (testedBaseUrl !== trimmedBaseUrl) {
      setValidationError(t('onboarding:fhirServerInfo.messages.connectionNotTested'));
      return;
    }

    setValidationError(null);

    patch('fhir', {
      fhirServerBaseUrl: trimmedBaseUrl,
      maxConcurrentRequests: maxConcurrentRequests!,
      maxRetries: maxRetries!,
      minAcquisitionPullTime: minPullTime,
      maxAcquisitionPullTime: maxPullTime,
      lagDuration: buildIso8601Duration(lagDays, lagHours, lagMinutes),
      connectionTested: testedBaseUrl === trimmedBaseUrl
    });
    setReadyToAdvance(true);
  }

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  const jwksInstructionsKey = vendorProfile?.documentKeys.jwksInstructions;
  const vendorDisplayName = vendorProfile?.displayName ?? '';

  return (
    <div className="fhir-server-info">
      <div className="card">
        <div className="card-scroll" ref={cardScrollRef}>
          <PageHeader title={t('onboarding:fhirServerInfo.title')} />
          <p className="subtitle">
            {t('onboarding:fhirServerInfo.subtitlePrefix')}{' '}
            <a href="https://hl7.org/fhir/R4/summary.html" target="_blank" rel="noreferrer">
              {t('onboarding:fhirServerInfo.subtitleFhirLinkText')}
            </a>{' '}
            {t('onboarding:fhirServerInfo.subtitleSuffix')}
          </p>

          <TextField
            id="fhirBaseUrl"
            type="url"
            label={t('onboarding:fhirServerInfo.fields.baseUrlLabel')}
            hint={t('onboarding:fhirServerInfo.fields.baseUrlTooltip')}
            placeholder={t('onboarding:fhirServerInfo.fields.baseUrlPlaceholder')}
            required
            value={baseUrl}
            error={baseUrlError ?? undefined}
            onChange={handleBaseUrlChange}
            onBlur={handleBaseUrlBlur} />

          {jwksInstructionsKey && (
            <>
              <div className="section-title">{t('onboarding:fhirServerInfo.authenticationSectionTitle')}</div>
              <div className="instructions-box">
                <p>{t('onboarding:fhirServerInfo.fields.jwksInstructions', {vendor: vendorDisplayName})}</p>
                <a
                  className="download-link"
                  href={api.getJwksInstructionsUrl(vendorProfile.vendor)}
                  download={`${vendorProfile.vendor}_JWKS_Instructions.pdf`}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 3v12" />
                    <path d="M7 10l5 5 5-5" />
                    <path d="M5 21h14" />
                  </svg>
                  {t('onboarding:fhirServerInfo.fields.downloadPdfInstructions')}
                </a>
              </div>
            </>
          )}

          <div className="section-title">{t('onboarding:fhirServerInfo.throttleSectionTitle')}</div>
          <div className="triplet">
            <NumberField
              id="maxConcurrentRequests"
              label={t('onboarding:fhirServerInfo.fields.maxConcurrentRequestsLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxConcurrentRequestsTooltip')}
              required
              step={1}
              value={maxConcurrentRequests}
              error={maxConcurrentRequestsError ?? undefined}
              onChange={setMaxConcurrentRequests}
              onBlur={() => validateField('maxConcurrentRequests', setMaxConcurrentRequestsError)} />
            <NumberField
              id="maxRetries"
              label={t('onboarding:fhirServerInfo.fields.maxRetriesLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxRetriesTooltip')}
              required
              step={1}
              value={maxRetries}
              error={maxRetriesError ?? undefined}
              onChange={setMaxRetries}
              onBlur={() => validateField('maxRetries', setMaxRetriesError)} />
          </div>

          <div className="triplet">
            <TextField
              id="minPullTime"
              label={t('onboarding:fhirServerInfo.fields.minPullTimeLabel')}
              hint={t('onboarding:fhirServerInfo.fields.minPullTimeTooltip')}
              placeholder={t('onboarding:fhirServerInfo.fields.pullTimePlaceholder')}
              maxLength={5}
              required
              value={minPullTime}
              error={minPullTimeError ?? undefined}
              onChange={setMinPullTime}
              onBlur={() => handlePullTimeBlur(minPullTime, setMinPullTime, 'minAcquisitionPullTime', setMinPullTimeError)} />
            <TextField
              id="maxPullTime"
              label={t('onboarding:fhirServerInfo.fields.maxPullTimeLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxPullTimeTooltip')}
              placeholder={t('onboarding:fhirServerInfo.fields.pullTimePlaceholder')}
              maxLength={5}
              required
              value={maxPullTime}
              error={maxPullTimeError ?? undefined}
              onChange={setMaxPullTime}
              onBlur={() => handlePullTimeBlur(maxPullTime, setMaxPullTime, 'maxAcquisitionPullTime', setMaxPullTimeError)} />
          </div>

          <div className="form-group">
            <label>
              {t('onboarding:fhirServerInfo.fields.lagLabel')}
              <span className="info-icon" aria-label={t('onboarding:fhirServerInfo.fields.lagLabel')}>
                ?<span className="tooltip-bubble" role="tooltip">{t('onboarding:fhirServerInfo.fields.lagTooltip')}</span>
              </span>
            </label>
            <div className="triplet">
              <NumberField
                id="lagDays"
                label={t('onboarding:fhirServerInfo.fields.lagDaysLabel')}
                required
                step={1}
                value={lagDays}
                error={lagDaysError ?? undefined}
                onChange={setLagDays}
                onBlur={() => validateField('lagDays', setLagDaysError)} />
              <NumberField
                id="lagHours"
                label={t('onboarding:fhirServerInfo.fields.lagHoursLabel')}
                required
                step={1}
                value={lagHours}
                error={lagHoursError ?? undefined}
                onChange={setLagHours}
                onBlur={() => validateField('lagHours', setLagHoursError)} />
              <NumberField
                id="lagMinutes"
                label={t('onboarding:fhirServerInfo.fields.lagMinutesLabel')}
                required
                step={1}
                value={lagMinutes}
                error={lagMinutesError ?? undefined}
                onChange={setLagMinutes}
                onBlur={() => validateField('lagMinutes', setLagMinutesError)} />
            </div>
          </div>

          {validationError && (
            <p className="nhsn-link__form-error" role="alert">
              {validationError}
            </p>
          )}

          {(testing || testResult) && (
            <div className="fhir-test-result">
              {testing ? (
                <span className="result-spinner" />
              ) : (
                <span className={`result-icon ${testResult!.success ? 'result-icon-success' : 'result-icon-failed'}`}>
                  {testResult!.success ? '✓' : '!'}
                </span>
              )}
              <span>{testing ? t('onboarding:fhirServerInfo.messages.testing') : testResult!.message}</span>
            </div>
          )}
        </div>

        <StepActions>
          <Button variant="secondary" onClick={onBack}>
            {t('common:actions.back')}
          </Button>
          <Button onClick={handleTestConnection} disabled={testing}>
            {t('common:actions.testConnection')}
          </Button>
          <Button onClick={handleNext} disabled={saving}>
            {t('common:actions.continue')}
          </Button>
        </StepActions>
      </div>
    </div>
  );
}

export default FhirStep;

function normalizePullTime(value: string): string {
  const digits = value.replace(/[^0-9]/g, '').slice(0, 4);
  if (!digits) {
    return '';
  }

  const raw = digits.length > 2 ? `${digits.slice(0, 2)}:${digits.slice(2)}` : digits;
  const match = raw.match(/^(\d{1,2}):?(\d{0,2})$/);
  if (!match) {
    return '';
  }

  const hours = Math.min(23, parseInt(match[1], 10) || 0);
  const minutes = Math.min(59, parseInt(match[2] || '0', 10) || 0);
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

function buildIso8601Duration(days?: number, hours?: number, minutes?: number): string {
  const totalMinutes = (days ?? 0) * 24 * 60 + (hours ?? 0) * 60 + (minutes ?? 0);
  const normalizedDays = Math.floor(totalMinutes / (24 * 60));
  const normalizedHours = Math.floor((totalMinutes % (24 * 60)) / 60);
  const normalizedMinutes = totalMinutes % 60;
  return `P${normalizedDays}DT${normalizedHours}H${normalizedMinutes}M`;
}
