import type {UserInfoResponse} from '../api/contracts';
import {STEP_VIEWS, stepIndex, visibleSteps} from './flow';
import type {FacilityDraft, StepTarget} from './types';
import {isStepId} from './types';

/**
 * The single gate every navigation passes through — reload, deep link,
 * popstate and Next alike.
 *
 * The URL can therefore only select among steps the draft already unlocked. A
 * corrupt or stale URL lands on the nearest legal step rather than failing,
 * and a sub-view whose params no longer resolve degrades to its step rather
 * than to an error.
 */
export function resolveStep(
  target: StepTarget | undefined,
  draft: FacilityDraft,
  user: UserInfoResponse
): StepTarget {
  const steps = visibleSteps(draft, user);
  const furthest = furthestLegalStep(draft, user);

  if (!target || !isStepId(target.stepId)) {
    return {stepId: furthest};
  }

  const step = steps.find(s => s.id === target.stepId);
  if (!step) {
    // In the flow yesterday, gone today, or never visible for this user.
    return {stepId: furthest};
  }

  if (!isUnlocked(target.stepId, draft, user)) {
    return {stepId: furthest};
  }

  const view = target.view;
  if (!view) {
    return {stepId: target.stepId};
  }

  const allowed = STEP_VIEWS[target.stepId] ?? [];
  if (view.stepId !== target.stepId || !allowed.includes(view.view)) {
    return {stepId: target.stepId};
  }

  return {stepId: target.stepId, view};
}

/**
 * A step is reachable when it is explicitly unlocked and every visible step
 * before it is complete. Both conditions matter: unlocking records where the
 * user has been, completeness stops a stale unlock from skipping a
 * prerequisite the user later cleared.
 */
export function isUnlocked(
  stepId: FacilityDraft['currentStepId'],
  draft: FacilityDraft,
  user: UserInfoResponse
): boolean {
  if (!draft.unlockedStepIds.includes(stepId)) {
    return false;
  }
  const steps = visibleSteps(draft, user);
  const index = steps.findIndex(s => s.id === stepId);
  if (index <= 0) {
    return index === 0;
  }
  return steps.slice(0, index).every(step => step.isComplete(draft));
}

/** The furthest step the draft legitimately reaches — the fallback for a rejected target. */
export function furthestLegalStep(
  draft: FacilityDraft,
  user: UserInfoResponse
): FacilityDraft['currentStepId'] {
  const steps = visibleSteps(draft, user);
  let furthest = steps[0]?.id ?? 'welcome';
  for (const step of steps) {
    if (isUnlocked(step.id, draft, user)) {
      furthest = step.id;
    } else {
      break;
    }
  }
  return furthest;
}

/** The next visible step after `stepId`, or undefined at the end of the flow. */
export function nextStepId(
  stepId: FacilityDraft['currentStepId'],
  draft: FacilityDraft,
  user: UserInfoResponse
): FacilityDraft['currentStepId'] | undefined {
  const steps = visibleSteps(draft, user);
  const index = steps.findIndex(s => s.id === stepId);
  return index >= 0 ? steps[index + 1]?.id : undefined;
}

export function previousStepId(
  stepId: FacilityDraft['currentStepId'],
  draft: FacilityDraft,
  user: UserInfoResponse
): FacilityDraft['currentStepId'] | undefined {
  const steps = visibleSteps(draft, user);
  const index = steps.findIndex(s => s.id === stepId);
  return index > 0 ? steps[index - 1]?.id : undefined;
}

export {stepIndex};
