import React, {useMemo} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import {Button, MessageContainer, NHSNLoadingIndicator, PageHeader, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {migrateDraft} from '../../types';

/**
 * Enrollment Complete: the flow's terminal screen. Read-only recap of what was
 * submitted, mirroring the POC's `panel-complete` summary list, plus a way
 * out to the host page (the POC's "Return to Welcome").
 *
 * Reads its own copy of the draft from GET /onboarding rather than the
 * OnboardingProvider store, so the recap reflects what the BFF actually
 * persisted for this facility rather than whatever local state survived the
 * final save.
 */
export function CompleteStep(_props: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {user, vendorProfile, goHome} = useOnboarding();
  const api = useApiClient();
  const {
    data: envelope,
    error: loadError
  } = useQuery({
    queryKey: ['completeStepDraft'],
    queryFn: () => api.getDraft()
  });
  const error = loadError ? (loadError instanceof Error ? loadError.message : String(loadError)) : undefined;

  const draft = useMemo(() => migrateDraft(envelope?.draft ?? null), [envelope]);
  const commitState = envelope?.commitState ?? null;

  const rows = useMemo(() => {
    const censusMethod =
      vendorProfile?.censusAcquisition === 'Sftp'
        ? draft.census.sftpHost ?? t('onboarding:complete.notAvailable')
        : vendorProfile?.displayName ?? draft.facilityInfo.vendor ?? t('onboarding:complete.notAvailable');

    return [
      {label: t('onboarding:complete.summary.facilityId'), value: commitState?.facilityId ?? user.facilityId ?? t('onboarding:complete.notAvailable')},
      {label: t('onboarding:complete.summary.vendor'), value: vendorProfile?.displayName ?? draft.facilityInfo.vendor ?? t('onboarding:complete.notAvailable')},
      {label: t('onboarding:complete.summary.timeZone'), value: draft.facilityInfo.timeZone ?? t('onboarding:complete.notAvailable')},
      {label: t('onboarding:complete.summary.fhirBaseUrl'), value: draft.fhir.fhirServerBaseUrl ?? t('onboarding:complete.notAvailable')},
      {
        label: t('onboarding:complete.summary.connectionTest'),
        value: draft.fhir.connectionTested
          ? t('onboarding:complete.connectionTested')
          : t('onboarding:complete.connectionNotTested')
      },
      {label: t('onboarding:complete.summary.censusMethod'), value: censusMethod},
      {label: t('onboarding:complete.summary.reportsGenerated'), value: draft.report.measures?.length ?? 0},
      {
        label: t('onboarding:complete.summary.completedReportId'),
        value: draft.report.lastRequestedReportId ?? t('onboarding:complete.notAvailable')
      }
    ];
  }, [draft, user, vendorProfile, commitState, t]);

  if (!envelope && !error) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="nhsn-link__content nhsn-link__complete">
      <PageHeader title={t('onboarding:complete.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:complete.subtitle')}</p>

      {error && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{error}</span>
        </MessageContainer>
      )}

      <ul className="nhsn-link__summary-list">
        {rows.map(row => (
          <li key={row.label}>
            <span>{row.label}</span>
            <span>{row.value}</span>
          </li>
        ))}
      </ul>

      <StepActions>
        <Button onClick={goHome}>{t('common:actions.returnToHome')}</Button>
      </StepActions>
    </div>
  );
}

export default CompleteStep;
