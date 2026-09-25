import type {MrnIntake} from '../api/contracts';
import type {
  CensusDraft,
  EncounterDraft,
  FacilityDraft,
  FacilityInfoDraft,
  FhirDraft,
  HslocDraft,
  LocationOrgDraft,
  ManualUploadDraft,
  ReportDraft,
  ReportResultsDraft,
  ReportingPlanDraft,
  StepId,
  StepView
} from './types';

/**
 * The sections a step may patch. Keeping this a closed union means a step
 * cannot reach into another step's slice, which is what keeps the single
 * reducer safe to share.
 */
export interface DraftSections {
  facilityInfo: FacilityInfoDraft;
  manualUpload: ManualUploadDraft;
  fhir: FhirDraft;
  census: CensusDraft;
  locationOrg: LocationOrgDraft;
  hsloc: HslocDraft;
  encounter: EncounterDraft;
  report: ReportDraft;
  reportResults: ReportResultsDraft;
  reportingPlan: ReportingPlanDraft;
}

export type DraftAction =
  | {type: 'draft/loaded'; draft: FacilityDraft}
  | {
      type: 'section/patch';
      section: keyof DraftSections;
      patch: Partial<DraftSections[keyof DraftSections]>;
    }
  | {type: 'step/goto'; stepId: StepId}
  | {type: 'step/unlock'; stepId: StepId}
  | {type: 'view/open'; view: StepView}
  | {type: 'view/close'}
  | {type: 'mrn/mirror'; intake: MrnIntake};

export function draftReducer(state: FacilityDraft, action: DraftAction): FacilityDraft {
  switch (action.type) {
    case 'draft/loaded':
      return action.draft;

    case 'section/patch':
      return {
        ...state,
        [action.section]: {...state[action.section], ...action.patch}
      };

    case 'step/goto': {
      if (state.currentStepId === action.stepId && !state.currentView) {
        return state;
      }
      const {currentView: _dropped, ...rest} = state;
      return {
        ...rest,
        currentStepId: action.stepId,
        unlockedStepIds: withStep(state.unlockedStepIds, action.stepId)
      };
    }

    case 'step/unlock':
      return state.unlockedStepIds.includes(action.stepId)
        ? state
        : {...state, unlockedStepIds: withStep(state.unlockedStepIds, action.stepId)};

    case 'view/open':
      return {...state, currentStepId: action.view.stepId, currentView: action.view};

    case 'view/close': {
      if (!state.currentView) {
        return state;
      }
      const {currentView: _closed, ...rest} = state;
      return rest;
    }

    case 'mrn/mirror':
      // Mirror only. The MRN step writes through the API; this copy exists so
      // reload and export render without a second round trip.
      return {...state, mrnIntake: action.intake};

    default:
      return state;
  }
}

function withStep(ids: StepId[], stepId: StepId): StepId[] {
  return ids.includes(stepId) ? ids : [...ids, stepId];
}
