import React from 'react';
import {useTranslation} from 'react-i18next';
import {Button, PageHeader, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';

/**
 * Step scaffold. The screen's own fields, validation and API calls are LEGLINK story: report results and the Report Details sub-view.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */
export function ReportResultsStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {saving} = useOnboarding();

  return (
    <div className="nhsn-link__content">
      <PageHeader title={t('onboarding:reportResults.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:messages.stepNotImplemented')}</p>

      <StepActions saving={saving}>
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={onNext} disabled={saving} loading={saving}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default ReportResultsStep;
