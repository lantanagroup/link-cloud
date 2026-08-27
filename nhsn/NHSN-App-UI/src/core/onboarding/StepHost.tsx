import React, {Suspense} from 'react';
import {useTranslation} from 'react-i18next';
import {NHSNLoadingIndicator} from '../fields';
import {getStep, visibleSteps} from './flow';
import {isUnlocked} from './gating';
import {useOnboarding} from './OnboardingProvider';

/**
 * Renders the current step and the step rail beside it.
 *
 * Steps are code-split, so the initial artifact carries the machine and the
 * first screen rather than all thirteen.
 */
export function StepHost() {
  const {t} = useTranslation(['onboarding', 'common']);
  const {loadState, error, draft, user, target, goTo, saving} =
    useOnboarding();

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
  const steps = visibleSteps(draft, user);

  return (
    <div className="nhsn-link__onboarding">
      <ol className="nhsn-link__steps">
        {steps.map((entry, index) => {
          const unlocked = isUnlocked(entry.id, draft, user);
          const complete = entry.isComplete(draft) && unlocked && entry.id !== target.stepId;
          return (
            <li
              key={entry.id}
              className={[
                'nhsn-link__step',
                entry.id === target.stepId ? 'nhsn-link__step--current' : '',
                complete ? 'nhsn-link__step--done' : ''
              ]
                .filter(Boolean)
                .join(' ')}>
              <button
                type="button"
                className="nhsn-link__step-button"
                disabled={!unlocked}
                onClick={() => goTo(entry.id)}>
                <span className="nhsn-link__step-index">{index + 1}</span>
                <span className="nhsn-link__step-label">{t(entry.labelKey)}</span>
              </button>
            </li>
          );
        })}
      </ol>

      {/* No conflict banner - the BFF scopes each save to its own step, so there's nothing to conflict. */}
      <section className="nhsn-link__step-panel" aria-live="polite">

        {saving && <span className="nhsn-link__saving">{t('common:status.saving')}</span>}

        {step ? (
          <Suspense fallback={<NHSNLoadingIndicator />}>
            <StepBody />
          </Suspense>
        ) : (
          <div className="nhsn-link__state">{t('onboarding:messages.stepUnavailable')}</div>
        )}
      </section>
    </div>
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
