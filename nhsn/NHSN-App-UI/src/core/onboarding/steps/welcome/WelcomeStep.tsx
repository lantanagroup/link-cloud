import React, {useMemo} from 'react';
import {Trans, useTranslation} from 'react-i18next';
import {Button, NewTabAnnouncement, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';

/**
 * The flow's intro screen.
 *
 * Deliberately the one step implemented alongside the skeleton: it exercises
 * every seam — flow, gating, provider, StepHost, fields, draft save and URL
 * sync — without owning any of the configuration the per-step stories cover.
 */
export function WelcomeStep({onNext}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {saving, user} = useOnboarding();

  const stableOnNext = useStableCallback(onNext);

  useStepChrome(
    useMemo(
      () => ({
        title: t('onboarding:welcome.title'),
        footer: (
          <StepActions saving={saving}>
            <Button onClick={stableOnNext} disabled={saving} loading={saving}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        )
      }),
      [t, saving, stableOnNext]
    )
  );

  return (
    <>
      <p className="nhsn-link__subtitle">
        <Trans
          t={t}
          i18nKey="onboarding:welcome.intro"
          components={{
            nhsnlink: (
              <a
                href="https://www.cdc.gov/nhsn/fhirportal/about.html"
                target="_blank"
                rel="noreferrer"
                aria-describedby="welcome-nhsnlink-link-hint"
              />
            ),
            fhir: (
              <a
                href="https://www.hl7.org/fhir/R4/index.html"
                target="_blank"
                rel="noreferrer"
                aria-describedby="welcome-fhir-link-hint"
              />
            )
          }}
        />
        <NewTabAnnouncement id="welcome-nhsnlink-link-hint" />
        <NewTabAnnouncement id="welcome-fhir-link-hint" />
      </p>

      <h2>{t('onboarding:welcome.audienceTitle')}</h2>
      <p className="nhsn-link__subtitle">{t('onboarding:welcome.audienceBody')}</p>

      <h2>{t('onboarding:welcome.workflowTitle')}</h2>
      <p className="nhsn-link__subtitle">{t('onboarding:welcome.workflowBody')}</p>
    </>
  );
}

export default WelcomeStep;
