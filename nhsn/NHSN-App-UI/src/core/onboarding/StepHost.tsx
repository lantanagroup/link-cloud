import { Suspense, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AcronymText, NHSNLoadingIndicator, PageHeader } from '../fields';
import { getStep, visibleSteps } from './flow';
import { isUnlocked } from './gating';
import { useOnboarding } from './OnboardingProvider';
import { StepChromeContext } from './StepChrome';
import type { StepChrome } from './StepChrome';

export function OnboardingStepsNav() {
  const { t } = useTranslation(['onboarding', 'common']);
  const { draft, user, target, goTo, saving, vendorProfile } = useOnboarding();
  const steps = visibleSteps(draft, user);

  return (
    <div className="nhsn-link__nav-section">
      <h3 className="nhsn-link__nav-section-heading">
        {t('common:navigation.onboarding')}
      </h3>

      <ol className="nhsn-link__steps">
        {steps.map((entry, index) => {
          const unlocked = isUnlocked(entry.id, draft, user, vendorProfile);
          const complete =
            entry.isComplete(draft, user, vendorProfile) && unlocked && entry.id !== target.stepId;
          return (
            <li
              key={entry.id}
              className={[
                'nhsn-link__step',
                unlocked ? 'nhsn-link__step--unlocked' : '',
                entry.id === target.stepId ? 'nhsn-link__step--current' : '',
                complete ? 'nhsn-link__step--done' : '',
              ]
                .filter(Boolean)
                .join(' ')}>
              <button
                type="button"
                className="nhsn-link__step-button"
                disabled={!unlocked || saving}
                aria-current={entry.id === target.stepId ? 'step' : undefined}
                onClick={() => goTo(entry.id)}>
                <span className="nhsn-link__step-index">{index + 1}</span>
                <span className="nhsn-link__step-label">
                  <AcronymText>{t(entry.labelKey)}</AcronymText>
                  {complete && (
                    <span className="nhsn-link__visually-hidden">
                      {' '}
                      {t('common:status.stepComplete')}
                    </span>
                  )}
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

export function StepHost() {
  const { t } = useTranslation(['onboarding', 'common']);
  const { loadState, error, target } = useOnboarding();
  const [chrome, setChrome] = useState<StepChrome | null>(null);

  if (loadState === 'loading') {
    return <NHSNLoadingIndicator />;
  }

  if (loadState === 'error') {
    return (
      <div className="nhsn-link__state nhsn-link__state--error" role="alert">
        {error ?? t('common:errors.unexpected')}
      </div>
    );
  }

  const step = getStep(target.stepId);

  return (
    <section className="nhsn-link__step-panel" aria-live="polite">
      {step ? (
        <div className={chrome ? 'nhsn-link__content' : 'nhsn-link__content nhsn-link__content--bare'}>
          {chrome && <PageHeader title={chrome.title} />}
          <StepChromeContext.Provider value={setChrome}>
            <Suspense fallback={<NHSNLoadingIndicator />}>
              <StepBody />
            </Suspense>
          </StepChromeContext.Provider>
          {chrome?.footer}
        </div>
      ) : (
        <div className="nhsn-link__state">
          {t('onboarding:messages.stepUnavailable')}
        </div>
      )}
    </section>
  );
}

function StepBody() {
  const { target, goNext, goBack } = useOnboarding();
  const step = getStep(target.stepId);
  if (!step) {
    return null;
  }
  const { Component } = step;
  return <Component onNext={goNext} onBack={goBack} />;
}
