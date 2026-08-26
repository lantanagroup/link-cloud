import {describe, expect, it} from 'vitest';
import bundledCommon from './bundled/common.json';
import bundledOnboarding from './bundled/onboarding.json';

/**
 * The bundled en-US copy is the component's last line of defence: if the BFF
 * is unreachable, these are the only strings it has. A missing key here means
 * a raw i18n key rendered inside the CDC NHSN App.
 *
 * This asserts the keys the shipped UI actually reads. It is not a
 * completeness check against the BFF's files — drift there only weakens the
 * offline fallback, and the served strings still win.
 */
const REQUIRED_COMMON = [
  'actions.continue',
  'actions.back',
  'actions.reload',
  'status.saving',
  'errors.unexpected',
  'navigation.home',
  'navigation.onboarding',
  'navigation.facility',
  'app.linkTitle',
  'state.loadingUserContext',
  'state.noUserContext',
  'auth.missingAccessTitle',
  'auth.missingFacilityTitle'
];

const REQUIRED_ONBOARDING = [
  'welcome.title',
  'welcome.intro',
  'welcome.audienceTitle',
  'welcome.workflowTitle',
  'messages.stepNotImplemented',
  'messages.stepUnavailable',
  'messages.draftConflict'
];

/** Every step's rail label and stub title, derived from the flow's own ids. */
const STEP_KEYS = [
  'welcome',
  'reportingPlan',
  'facilityInfo',
  'manualUpload',
  'fhir',
  'census',
  'locationOrg',
  'hsloc',
  'encounter',
  'report',
  'reportResults',
  'mrnIntake',
  'complete'
];

function lookup(source: unknown, path: string): unknown {
  return path.split('.').reduce<unknown>((node, key) => {
    return node && typeof node === 'object' ? (node as Record<string, unknown>)[key] : undefined;
  }, source);
}

describe('bundled en-US fallback', () => {
  it.each(REQUIRED_COMMON)('common.json defines %s', key => {
    expect(lookup(bundledCommon, key)).toBeTypeOf('string');
  });

  it.each(REQUIRED_ONBOARDING)('onboarding.json defines %s', key => {
    expect(lookup(bundledOnboarding, key)).toBeTypeOf('string');
  });

  it.each(STEP_KEYS)('onboarding.json defines a rail label for %s', key => {
    expect(lookup(bundledOnboarding, `steps.${key}`)).toBeTypeOf('string');
  });

  it.each(STEP_KEYS)('onboarding.json defines a title for %s', key => {
    expect(lookup(bundledOnboarding, `${key}.title`)).toBeTypeOf('string');
  });
});
