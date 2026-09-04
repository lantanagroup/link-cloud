import React, {Suspense} from 'react';
import {useTranslation} from 'react-i18next';
import {NHSNLoadingIndicator} from '../fields';
import {getStep, visibleSteps} from './flow';
import {isUnlocked} from './gating';
import {useOnboarding} from './OnboardingProvider';

/**
 * The onboarding step rail: lives inside the app's single sidebar (see
 * `NavigationRail`'s `stepsSection`) rather than drawing a nav surface of its
 * own, so onboarding progress and app navigation never compete for space.
 */
export function OnboardingStepsNav() {
  const {t} = useTranslation(['onboarding', 'common']);
  const {draft, user, target, goTo, saving} = useOnboarding();
  const steps = visibleSteps(draft, user);

  return (
    <div className="nhsn-link__nav-section">
      <h3 className="nhsn-link__nav-section-heading">{t('common:navigation.onboarding')}</h3>

      <ol className="nhsn-link__steps">
        {steps.map((entry, index) => {
          const unlocked = isUnlocked(entry.id, draft, user);
          const complete = entry.isComplete(draft) && unlocked && entry.id !== target.stepId;
          return (
            <li
              key={entry.id}
              className={[
                'nhsn-link__step',
                unlocked ? 'nhsn-link__step--unlocked' : '',
                entry.id === target.stepId ? 'nhsn-link__step--current' : '',
                complete ? 'nhsn-link__step--done' : ''
              ]
                .filter(Boolean)
                .join(' ')}>
              <button
                type="button"
                className="nhsn-link__step-button"
                disabled={!unlocked || saving}
                onClick={() => goTo(entry.id)}>
                <span className="nhsn-link__step-index">{index + 1}</span>
                <span className="nhsn-link__step-label">{t(entry.labelKey)}</span>
              </button>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

/**
 * Renders the current step's panel. The step rail beside it now lives in the
 * app's single sidebar (`OnboardingStepsNav`) rather than here.
 *
 * Steps are code-split, so the initial artifact carries the machine and the
 * first screen rather than all thirteen.
 */
export function StepHost() {
  const {t} = useTranslation(['onboarding', 'common']);
  const {loadState, error, target} = useOnboarding();

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
    // No conflict banner - the BFF scopes each save to its own step, so there's nothing to conflict.
    <section className="nhsn-link__step-panel" aria-live="polite">
      {step ? (
        <Suspense fallback={<NHSNLoadingIndicator />}>
          <StepBody />
        </Suspense>
      ) : (
        <div className="nhsn-link__state">{t('onboarding:messages.stepUnavailable')}</div>
      )}
    </section>
  );
}

function StepBody() {
  const {target, goNext, goBack} = useOnboarding();
  const step = getStep(target.stepId);
  if (!step) {
    return null;
  }
  const {Component} = step;
  return <Component onNext={goNext} onBack={goBack} />;
}
