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
  const {loadState, error, draft, user, target, goTo, saving} = useOnboarding();

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
      <aside className="nhsn-link__step-nav">
        <h1 className="nhsn-link__step-nav-title">{t('common:app.linkTitle')}</h1>
        <p className="nhsn-link__step-nav-subtitle">{t('common:navigation.onboarding')}</p>

        {(user.facilityName || user.facilityId) && (
          <div className="nhsn-link__step-nav-facility">
            {user.facilityName && (
              <div className="nhsn-link__step-nav-facility-name">{user.facilityName}</div>
            )}
            {user.facilityId && (
              <div className="nhsn-link__step-nav-facility-id">{user.facilityId}</div>
            )}
          </div>
        )}

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
      </aside>

      {/* No conflict banner - the BFF scopes each save to its own step, so there's nothing to conflict. */}
      <section className="nhsn-link__step-panel" aria-live="polite">

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
