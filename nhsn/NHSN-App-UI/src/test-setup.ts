import i18n from 'i18next';
import {initReactI18next} from 'react-i18next';
import {cleanup} from '@testing-library/react';
import {afterEach} from 'vitest';

/**
 * Testing Library auto-registers cleanup only when Vitest globals are on. They
 * are off here, so unmounting is explicit — without it every rendered tree
 * stays in the document and `screen` queries match the previous test's app as
 * well as the current one.
 */
afterEach(cleanup);

/**
 * Component tests initialize i18n with inline resources rather than the HTTP
 * backend the app uses: the real loader fetches from the BFF, which is exactly
 * the dependency these tests exist to avoid.
 *
 * Only the keys assertions depend on are listed. A missing key renders as the
 * key itself, which is visible in a failure rather than silently blank.
 */
void i18n.use(initReactI18next).init({
  lng: 'en-US',
  fallbackLng: false,
  ns: ['common', 'onboarding'],
  defaultNS: 'common',
  interpolation: {escapeValue: false},
  resources: {
    'en-US': {
      common: {
        actions: {continue: 'Continue', back: 'Back', reload: 'Reload'},
        status: {saving: 'Saving…'},
        errors: {unexpected: 'Something went wrong.'},
        navigation: {
          facility: 'Facility',
          home: 'Home',
          onboarding: 'Onboarding',
          configuration: 'Configuration'
        },
        app: {linkTitle: 'NHSNLink'},
        state: {loadingUserContext: 'Loading…', noUserContext: 'No user context'}
      },
      onboarding: {
        steps: {
          welcome: 'Welcome',
          reportingPlan: 'Facility Reporting Plan',
          facilityInfo: 'Facility Information',
          manualUpload: 'Manual Upload',
          fhir: 'FHIR Server Information',
          census: 'Patients of Interest Configuration',
          locationOrg: 'Organization Identification',
          hsloc: 'Location Identification (HSLOC)',
          encounter: 'Encounter Mapping',
          report: 'Generate Test Report',
          reportResults: 'Report Results',
          mrnIntake: 'MRN Identifier Intake',
          complete: 'Enrollment Complete'
        },
        welcome: {
          title: 'Welcome to DQM Onboarding',
          intro: 'Intro copy.',
          audienceTitle: 'Intended Audience',
          audienceBody: 'Audience copy.',
          workflowTitle: 'Workflow',
          workflowBody: 'Workflow copy.',
          vendorNote: 'Your facility is configured for {{vendor}}.'
        },
        reportingPlan: {title: 'Facility Reporting Plan'},
        messages: {
          stepNotImplemented: 'This step is not implemented yet.',
          stepUnavailable: 'That step is not available.',
          draftConflict: 'Updated in another window.'
        }
      }
    }
  }
});
