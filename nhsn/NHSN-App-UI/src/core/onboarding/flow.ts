import React from 'react';
import type {UserInfoResponse} from '../api/contracts';
import {parseHoursMinutesDuration} from '../shared/duration';
import type {FacilityDraft, StepId} from './types';
import {STEP_IDS} from './types';
import {CENSUS_LIST_KEYS} from './steps/census/validate';
import {isFacilityInfoComplete} from './steps/facility-info/validate';
import {isFhirComplete} from './steps/fhir/validate';
import {isHslocComplete} from './steps/hsloc/validate';
import {isMrnIntakeComplete} from './steps/mrn-intake/validate';
import {isReportComplete} from './steps/report/validate';
import {WelcomeStep} from './steps/welcome/WelcomeStep';

export interface StepProps {
  /** Advance to the next visible step, unlocking it. */
  onNext: () => void;
  onBack: () => void;
}

export interface Step {
  id: StepId;
  /** i18n key, never a literal. */
  labelKey: string;
  Component: React.ComponentType<StepProps>;
  /** Drives unlock of the following step. `user` is optional; only census reads it. */
  isComplete: (draft: FacilityDraft, user?: UserInfoResponse) => boolean;
  /**
   * Reserved. No step uses it — all thirteen are always in the flow, and
   * vendor branching is field-level and driven by the VendorProfile the BFF
   * serves. Do not reach for this to implement vendor branching.
   */
  isVisible?: (draft: FacilityDraft, user: UserInfoResponse) => boolean;
}

const lazyStep = (loader: () => Promise<{default: React.ComponentType<StepProps>}>) =>
  React.lazy(loader);

/**
 * Step order is the POC's, with the facility picker removed — that is a
 * throwaway developer screen in `shell/facilities`, not a step.
 */
export const STEPS: Step[] = [
  {
    id: 'welcome',
    labelKey: 'onboarding:steps.welcome',
    Component: WelcomeStep,
    isComplete: () => true
  },
  {
    id: 'reporting-plan',
    labelKey: 'onboarding:steps.reportingPlan',
    Component: lazyStep(() => import('./steps/reporting-plan/ReportingPlanStep')),
    isComplete: () => true
  },
  {
    id: 'facility-info',
    labelKey: 'onboarding:steps.facilityInfo',
    Component: lazyStep(() => import('./steps/facility-info/FacilityInfoStep')),
    isComplete: isFacilityInfoComplete
  },
  {
    id: 'manual-upload',
    labelKey: 'onboarding:steps.manualUpload',
    Component: lazyStep(() => import('./steps/manual-upload/ManualUploadStep')),
    // Optional by design - the step's own copy invites uploading a partially completed
    // form and finishing later, and ManualUploadStep has no useStepValidator gating Continue.
    isComplete: () => true
  },
  {
    id: 'fhir',
    labelKey: 'onboarding:steps.fhir',
    Component: lazyStep(() => import('./steps/fhir/FhirStep')),
    isComplete: isFhirComplete
  },
  {
    id: 'census',
    labelKey: 'onboarding:steps.census',
    Component: lazyStep(() => import('./steps/census/CensusStep')),
    isComplete: (draft, user) => {
      const c = draft.census;
      // No vendor name here - which branch is required is inferred from which
      // config the draft actually carries, since isComplete has no vendorProfile.
      const epicConfigured = CENSUS_LIST_KEYS.every(key => Boolean(c.patientListIds?.[key]?.trim()));
      const cernerConfigured = Boolean(c.sftpHost?.trim()) && c.sftpPort !== undefined;
      if (!epicConfigured && !cernerConfigured) {
        return false;
      }
      if (cernerConfigured && !c.sftpConnectionTested) {
        return false;
      }
      // Only require acknowledgement when the fetch is actually live - mirrors CensusStep.tsx's `validationLive`.
      const validationLive = epicConfigured
        ? Boolean(user?.capabilities?.patientListWithNames)
        : Boolean(user?.capabilities?.sftpFileListing);
      if (validationLive && !c.accuracyAcknowledged) {
        return false;
      }
      const frequency = parseHoursMinutesDuration(c.acquisitionFrequency);
      if (!frequency) {
        return false;
      }
      const totalMinutes = frequency.hours * 60 + frequency.minutes;
      return totalMinutes >= 5 && totalMinutes <= 1440;
    }
  },
  {
    id: 'location-org',
    labelKey: 'onboarding:steps.locationOrg',
    Component: lazyStep(() => import('./steps/location-org/LocationOrgStep')),
    // Nothing here is mandatory, matching the POC.
    isComplete: () => true
  },
  {
    id: 'hsloc',
    labelKey: 'onboarding:steps.hsloc',
    Component: lazyStep(() => import('./steps/hsloc/HslocStep')),
    isComplete: isHslocComplete
  },
  {
    id: 'encounter',
    labelKey: 'onboarding:steps.encounter',
    Component: lazyStep(() => import('./steps/encounter/EncounterStep')),
    isComplete: () => true
  },
  {
    id: 'report',
    labelKey: 'onboarding:steps.report',
    Component: lazyStep(() => import('./steps/report/ReportStep')),
    isComplete: isReportComplete
  },
  {
    id: 'report-results',
    labelKey: 'onboarding:steps.reportResults',
    Component: lazyStep(() => import('./steps/report-results/ReportResultsStep')),
    isComplete: draft => Boolean(draft.reportResults.accuracyAcknowledged)
  },
  {
    id: 'mrn-intake',
    labelKey: 'onboarding:steps.mrnIntake',
    Component: lazyStep(() => import('./steps/mrn-intake/MrnIntakeStep')),
    isComplete: draft => isMrnIntakeComplete(draft.mrnIntake)
  },
  {
    id: 'complete',
    labelKey: 'onboarding:steps.complete',
    Component: lazyStep(() => import('./steps/complete/CompleteStep')),
    isComplete: () => true
  }
];

const STEP_BY_ID = new Map(STEPS.map(step => [step.id, step]));

export function getStep(id: StepId): Step | undefined {
  return STEP_BY_ID.get(id);
}

export function visibleSteps(draft: FacilityDraft, user: UserInfoResponse): Step[] {
  return STEPS.filter(step => step.isVisible?.(draft, user) ?? true);
}

/** Views a step may drill into, checked by resolveStep before honouring a URL. */
export const STEP_VIEWS: Partial<Record<StepId, string[]>> = {
  'report-results': ['detail']
};

/** Declaration-order index, used to compare step positions. */
export function stepIndex(id: StepId): number {
  return STEP_IDS.indexOf(id);
}
