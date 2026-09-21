import React, {useEffect, useMemo, useRef, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import {AcronymText, Button, InfoTooltip, NewTabAnnouncement, NumberField, RequiredAsterisk, StepActions, TextField} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import {validateFhir, type FhirFieldValues, type FieldErrors} from './validate';
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
  const {draft, patch, saving, vendorProfile} = useOnboarding();
  const fhir = draft.fhir;
  const [initialLagDays, initialLagHours, initialLagMinutes] = parseIso8601Duration(fhir.lagDuration);

  const [validationError, setValidationError] = useState<string | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});

  function announceValidationMessage(message: string) {
    setValidationError(null);
    window.setTimeout(() => setValidationError(message), 0);
  }

  const [baseUrl, setBaseUrl] = useState(fhir.fhirServerBaseUrl ?? '');
  const [maxConcurrentRequests, setMaxConcurrentRequests] = useState<number | undefined>(fhir.maxConcurrentRequests);
  const [maxRetries, setMaxRetries] = useState<number | undefined>(fhir.maxRetries);
  const [minPullTime, setMinPullTime] = useState(fhir.minAcquisitionPullTime ?? '');
  const [maxPullTime, setMaxPullTime] = useState(fhir.maxAcquisitionPullTime ?? '');
  const [lagDays, setLagDays] = useState<number | undefined>(initialLagDays);
  const [lagHours, setLagHours] = useState<number | undefined>(initialLagHours);
  const [lagMinutes, setLagMinutes] = useState<number | undefined>(initialLagMinutes);

  const persistedTestedBaseUrl = fhir.connectionTested && fhir.fhirServerBaseUrl ? fhir.fhirServerBaseUrl.trim() : null;

  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{success: boolean; message: string} | null>(
    persistedTestedBaseUrl ? {success: true, message: t('onboarding:fhirServerInfo.messages.testSuccess')} : null
  );
  const [testedBaseUrl, setTestedBaseUrl] = useState<string | null>(persistedTestedBaseUrl);

  const testResultRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (testing || testResult) {
      scrollNearestContainerToBottom(testResultRef.current);
    }
  }, [testing, testResult]);

  const [readyToAdvance, setReadyToAdvance] = useState(false);

  useEffect(() => {
    if (readyToAdvance) {
      setReadyToAdvance(false);
      onNext();
    }
  }, [readyToAdvance, onNext]);

  // Any field on this page invalidates a prior Test Connection - not just the base URL. The
  // reachability check itself only depends on the URL, but the whole point of testing is "this
  // configuration is good to save," so touching anything means that promise needs re-confirming.
  function resetConnectionTest() {
    setTestedBaseUrl(null);
    setTestResult(null);
  }

  function handleBaseUrlChange(value: string) {
    setBaseUrl(value);
    resetConnectionTest();
    patch('fhir', {fhirServerBaseUrl: value});
  }

  function patchLagDuration(overrides: {lagDays?: number; lagHours?: number; lagMinutes?: number}) {
    patch('fhir', {
      lagDuration: buildIso8601Duration(
        overrides.lagDays ?? lagDays,
        overrides.lagHours ?? lagHours,
        overrides.lagMinutes ?? lagMinutes
      )
    });
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

  function markTouched(field: string) {
    setTouched(prev => ({...prev, [field]: true}));
  }

  function refreshErrors(overrides: Partial<FhirFieldValues> = {}) {
    setErrors(validateFhir(currentFieldValues(overrides)));
  }

  /** Gates a field's rendered error on it having been touched, translating the i18n key only once shown. */
  function fieldError(field: string): string | undefined {
    return touched[field] && errors[field] ? t(errors[field]) : undefined;
  }

  function handleBaseUrlBlur() {
    markTouched('fhirServerBaseUrl');
    refreshErrors();
  }

  function handlePullTimeBlur(value: string, setter: (value: string) => void, field: 'minAcquisitionPullTime' | 'maxAcquisitionPullTime') {
    const normalized = normalizePullTime(value);
    setter(normalized);
    markTouched(field);
    refreshErrors({[field]: normalized});
    patch('fhir', {[field]: normalized});
  }

  async function handleTestConnection() {
    const nextErrors = validateFhir(currentFieldValues());
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setTouched({
        fhirServerBaseUrl: true,
        maxConcurrentRequests: true,
        maxRetries: true,
        minAcquisitionPullTime: true,
        maxAcquisitionPullTime: true,
        lagDays: true,
        lagHours: true,
        lagMinutes: true
      });
      announceValidationMessage(t('onboarding:fhirServerInfo.messages.incomplete'));
      return;
    }
    setValidationError(null);

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
    setTouched({
      fhirServerBaseUrl: true,
      maxConcurrentRequests: true,
      maxRetries: true,
      minAcquisitionPullTime: true,
      maxAcquisitionPullTime: true,
      lagDays: true,
      lagHours: true,
      lagMinutes: true
    });
    const trimmedBaseUrl = baseUrl.trim();
    const nextErrors = validateFhir({
      fhirServerBaseUrl: baseUrl,
      maxConcurrentRequests,
      maxRetries,
      minAcquisitionPullTime: minPullTime,
      maxAcquisitionPullTime: maxPullTime,
      lagDays,
      lagHours,
      lagMinutes
    });
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      announceValidationMessage(t('onboarding:fhirServerInfo.messages.incomplete'));
      return;
    }

    if (testedBaseUrl !== trimmedBaseUrl) {
      announceValidationMessage(t('onboarding:fhirServerInfo.messages.connectionNotTested'));
      return;
    }

    setValidationError(null);

    patch('fhir', {
      fhirServerBaseUrl: trimmedBaseUrl,
      maxConcurrentRequests: maxConcurrentRequests!,
      maxRetries,
      minAcquisitionPullTime: minPullTime,
      maxAcquisitionPullTime: maxPullTime,
      lagDuration: buildIso8601Duration(lagDays, lagHours, lagMinutes),
      connectionTested: testedBaseUrl === trimmedBaseUrl
    });
    setReadyToAdvance(true);
  }

  const jwksInstructionsKey = vendorProfile?.documentKeys.jwksInstructions;
  const vendorDisplayName = vendorProfile?.displayName ?? '';
  const connectionVerified = testedBaseUrl !== null && testedBaseUrl === baseUrl.trim();
  const isFormValid = Object.keys(validateFhir(currentFieldValues())).length === 0;

  const stableOnBack = useStableCallback(onBack);
  const stableHandleTestConnection = useStableCallback(handleTestConnection);
  const stableHandleNext = useStableCallback(handleNext);

  useStepChrome(
    useMemo(
      () => ({
        title: t('onboarding:fhirServerInfo.title'),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving}>
              {t('common:actions.back')}
            </Button>
            <Button onClick={stableHandleTestConnection} disabled={testing}>
              {t('common:actions.testConnection')}
            </Button>
            <Button onClick={stableHandleNext} disabled={saving || !isFormValid || !connectionVerified} loading={saving}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        )
      }),
      [t, saving, stableOnBack, stableHandleTestConnection, testing, stableHandleNext, isFormValid, connectionVerified]
    )
  );

  return (
    <div className="fhir-server-info">
          <p className="subtitle">
            {t('onboarding:fhirServerInfo.subtitlePrefix')}{' '}
            <a href="https://hl7.org/fhir/R4/summary.html" target="_blank" rel="noreferrer">
              {t('onboarding:fhirServerInfo.subtitleFhirLinkText')}
              <NewTabAnnouncement />
            </a>{' '}
            <AcronymText>{t('onboarding:fhirServerInfo.subtitleSuffix')}</AcronymText>
          </p>

          <TextField
            id="fhirBaseUrl"
            type="url"
            label={t('onboarding:fhirServerInfo.fields.baseUrlLabel')}
            hint={t('onboarding:fhirServerInfo.fields.baseUrlTooltip')}
            placeholder={t('onboarding:fhirServerInfo.fields.baseUrlPlaceholder')}
            required
            value={baseUrl}
            error={fieldError('fhirServerBaseUrl')}
            onChange={handleBaseUrlChange}
            onBlur={handleBaseUrlBlur} />

          {jwksInstructionsKey && (
            <>
              <div className="section-title" id="fhir-jwks-section-title">
                {t('onboarding:fhirServerInfo.authenticationSectionTitle')}
              </div>
              <div className="instructions-box">
                <p id="fhir-jwks-instructions-desc">
                  <AcronymText>{t('onboarding:fhirServerInfo.fields.jwksInstructions', {vendor: vendorDisplayName})}</AcronymText>
                </p>
                <a
                  className="download-link"
                  href={api.getJwksInstructionsUrl(vendorProfile.vendor)}
                  target="_blank"
                  rel="noopener"
                  aria-describedby="fhir-jwks-section-title fhir-jwks-instructions-desc">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M12 3v12" />
                    <path d="M7 10l5 5 5-5" />
                    <path d="M5 21h14" />
                  </svg>
                  {t('onboarding:fhirServerInfo.fields.downloadPdfInstructions')}
                  <NewTabAnnouncement />
                </a>
              </div>
            </>
          )}

          <div className="section-title" id="fhir-throttle-section-title">
            {t('onboarding:fhirServerInfo.throttleSectionTitle')}
          </div>
          <div role="group" aria-labelledby="fhir-throttle-section-title">
          <div className="triplet">
            <NumberField
              id="maxConcurrentRequests"
              label={t('onboarding:fhirServerInfo.fields.maxConcurrentRequestsLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxConcurrentRequestsTooltip')}
              required
              min={1}
              max={8}
              step={1}
              value={maxConcurrentRequests}
              error={fieldError('maxConcurrentRequests')}
              onChange={value => {
                setMaxConcurrentRequests(value);
                markTouched('maxConcurrentRequests');
                refreshErrors({maxConcurrentRequests: value});
                resetConnectionTest();
                patch('fhir', {maxConcurrentRequests: value});
              }}
              onBlur={() => {
                markTouched('maxConcurrentRequests');
                refreshErrors();
              }} />
            <NumberField
              id="maxRetries"
              label={t('onboarding:fhirServerInfo.fields.maxRetriesLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxRetriesTooltip')}
              min={0}
              max={10}
              step={1}
              value={maxRetries}
              error={fieldError('maxRetries')}
              onChange={value => {
                setMaxRetries(value);
                markTouched('maxRetries');
                refreshErrors({maxRetries: value});
                resetConnectionTest();
                patch('fhir', {maxRetries: value});
              }}
              onBlur={() => {
                markTouched('maxRetries');
                refreshErrors();
              }} />
          </div>

          <div className="triplet">
            <TextField
              id="minPullTime"
              label={t('onboarding:fhirServerInfo.fields.minPullTimeLabel')}
              hint={t('onboarding:fhirServerInfo.fields.minPullTimeTooltip')}
              placeholder={t('onboarding:fhirServerInfo.fields.pullTimePlaceholder')}
              maxLength={5}
              value={minPullTime}
              error={fieldError('minAcquisitionPullTime')}
              onChange={value => {
                const normalized = digitsOnly(value);
                setMinPullTime(normalized);
                resetConnectionTest();
                patch('fhir', {minAcquisitionPullTime: normalized});
              }}
              onBlur={() => handlePullTimeBlur(minPullTime, setMinPullTime, 'minAcquisitionPullTime')} />
            <TextField
              id="maxPullTime"
              label={t('onboarding:fhirServerInfo.fields.maxPullTimeLabel')}
              hint={t('onboarding:fhirServerInfo.fields.maxPullTimeTooltip')}
              placeholder={t('onboarding:fhirServerInfo.fields.pullTimePlaceholder')}
              maxLength={5}
              value={maxPullTime}
              error={fieldError('maxAcquisitionPullTime')}
              onChange={value => {
                const normalized = digitsOnly(value);
                setMaxPullTime(normalized);
                resetConnectionTest();
                patch('fhir', {maxAcquisitionPullTime: normalized});
              }}
              onBlur={() => handlePullTimeBlur(maxPullTime, setMaxPullTime, 'maxAcquisitionPullTime')} />
          </div>
          </div>

          <div className="form-group" role="group" aria-labelledby="fhir-lag-duration-label">
            <label id="fhir-lag-duration-label">
              {t('onboarding:fhirServerInfo.fields.lagLabel')}
              <RequiredAsterisk />
              <InfoTooltip
                label={t('onboarding:fhirServerInfo.fields.lagLabel')}
                content={t('onboarding:fhirServerInfo.fields.lagTooltip')}
              />
            </label>
            <div className="triplet">
              <NumberField
                id="lagDays"
                label={t('onboarding:fhirServerInfo.fields.lagDaysLabel')}
                required
                min={0}
                max={30}
                step={1}
                value={lagDays}
                error={fieldError('lagDays')}
                onChange={value => {
                  setLagDays(value);
                  markTouched('lagDays');
                  refreshErrors({lagDays: value});
                  resetConnectionTest();
                  patchLagDuration({lagDays: value});
                }}
                onBlur={() => {
                  markTouched('lagDays');
                  refreshErrors();
                }} />
              <NumberField
                id="lagHours"
                label={t('onboarding:fhirServerInfo.fields.lagHoursLabel')}
                required
                min={0}
                max={23}
                step={1}
                value={lagHours}
                error={fieldError('lagHours')}
                onChange={value => {
                  setLagHours(value);
                  markTouched('lagHours');
                  refreshErrors({lagHours: value});
                  resetConnectionTest();
                  patchLagDuration({lagHours: value});
                }}
                onBlur={() => {
                  markTouched('lagHours');
                  refreshErrors();
                }} />
              <NumberField
                id="lagMinutes"
                label={t('onboarding:fhirServerInfo.fields.lagMinutesLabel')}
                required
                min={0}
                max={59}
                step={1}
                value={lagMinutes}
                error={fieldError('lagMinutes')}
                onChange={value => {
                  setLagMinutes(value);
                  markTouched('lagMinutes');
                  refreshErrors({lagMinutes: value});
                  resetConnectionTest();
                  patchLagDuration({lagMinutes: value});
                }}
                onBlur={() => {
                  markTouched('lagMinutes');
                  refreshErrors();
                }} />
            </div>
            <p className="k-form-error" role="alert">
              {touched.lagDays && touched.lagHours && touched.lagMinutes && errors.lagDuration
                ? t(errors.lagDuration)
                : null}
            </p>
          </div>

          <p className="nhsn-link__form-error" role="alert">
            {validationError}
          </p>

          {(testing || testResult) && (
            <div className="fhir-test-result" role="status" ref={testResultRef}>
              {testing ? (
                <span className="result-spinner" aria-hidden="true" />
              ) : (
                <span
                  className={`result-icon ${testResult!.success ? 'result-icon-success' : 'result-icon-failed'}`}
                  aria-hidden="true">
                  {testResult!.success ? '✓' : '!'}
                </span>
              )}
              <span>{testing ? t('onboarding:fhirServerInfo.messages.testing') : testResult!.message}</span>
            </div>
          )}
    </div>
  );
}

export default FhirStep;

function scrollNearestContainerToBottom(element: HTMLElement | null): void {
  let node = element?.parentElement ?? null;
  while (node) {
    const {overflowY} = window.getComputedStyle(node);
    if (overflowY === 'auto' || overflowY === 'scroll') {
      node.scrollTo({top: node.scrollHeight, behavior: 'smooth'});
      return;
    }
    node = node.parentElement;
  }
}

/** A letter can never be part of a valid HH:MM time, so it's blocked at every keystroke. */
function digitsOnly(value: string): string {
  return value.replace(/[^0-9]/g, '');
}

function normalizePullTime(value: string): string {
  const digits = value.replace(/[^0-9]/g, '').slice(0, 4);
  if (!digits) {
    return '';
  }
  if (digits.length <= 2) {
    return digits;
  }
  return `${digits.slice(0, 2)}:${digits.slice(2)}`;
}

function buildIso8601Duration(days?: number, hours?: number, minutes?: number): string {
  const totalMinutes = (days ?? 0) * 24 * 60 + (hours ?? 0) * 60 + (minutes ?? 0);
  const normalizedDays = Math.floor(totalMinutes / (24 * 60));
  const normalizedHours = Math.floor((totalMinutes % (24 * 60)) / 60);
  const normalizedMinutes = totalMinutes % 60;
  return `P${normalizedDays}DT${normalizedHours}H${normalizedMinutes}M`;
}

// Inverse of buildIso8601Duration, but tolerant of any minimal ISO-8601 duration shape, not just
// the one buildIso8601Duration itself emits: a value round-tripped through Query Dispatch comes
// back as .NET's XmlConvert.ToString(TimeSpan), which drops any component that's zero entirely -
// 50 days with no hours/minutes serializes as "P50D", not "P50DT0H0M" - so every one of D/T/H/M/S
// here is optional, and a missing piece just means zero, not "unparseable".
function parseIso8601Duration(duration?: string): [number | undefined, number | undefined, number | undefined] {
  if (!duration) {
    return [undefined, undefined, undefined];
  }

  const match = duration.match(/^P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:[\d.]+S)?)?$/);
  if (!match) {
    return [undefined, undefined, undefined];
  }

  return [Number(match[1] ?? 0), Number(match[2] ?? 0), Number(match[3] ?? 0)];
}
