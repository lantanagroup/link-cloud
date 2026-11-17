import {
  IRubricDetail,
  IRubricSettings,
  IRubricVersionSummary,
  RubricVersionStatus
} from '../../interfaces/validation/rubric.interface';
import { pipelineUse } from './rubric-display';

// What publishing, retiring and deleting a version do, worked out before the person confirms, so the
// confirmation can say it. The service decides in the end; this is what it will decide, as far as the
// page knows.

export type RubricAction = 'publish' | 'retire' | 'delete';

/** What a version in the status can have done to it. A draft is deleted, not retired; only a draft is published. */
export function actionsFor(status: RubricVersionStatus | null | undefined): RubricAction[] {
  switch (status) {
    case 'DRAFT':
      return ['publish', 'delete'];
    case 'PUBLISHED':
      return ['retire'];
    default:
      return [];
  }
}

export const ACTION_NAMES: Record<RubricAction, string> = {
  publish: 'Publish',
  retire: 'Retire',
  delete: 'Delete'
};

/** The version as the plan needs it: either a row of the rubric's versions or the version page's own. */
export type PlannedVersion =
  Pick<IRubricVersionSummary, 'semver' | 'status' | 'dryRunCompletedAt' | 'dryRunStatus'>;

export interface RubricActionPlan {
  action: RubricAction;
  /** "Publish version 1.1.0?" */
  heading: string;
  /** The word on the button that does it. */
  confirmLabel: string;
  /** What will happen, one statement each. */
  consequences: string[];
  /** What the person should know before going ahead. None of these stops it. */
  warnings: string[];
  /** Why it cannot be done; null when it can. */
  blocked: string | null;
  /** True when what blocks it is a missing dry run, so the page can offer one. */
  needsDryRun: boolean;
  /** What must be typed to go ahead; null when choosing the button is enough. */
  typeToConfirm: string | null;
}

/**
 * The version that would be active once `semver` is retired: the most recently published of the others.
 * Undefined when none would be left.
 */
export function activeAfterRetiring(rubric: IRubricDetail | null | undefined, semver: string): string | undefined {
  return (rubric?.versions ?? [])
    .filter(version => version.status === 'PUBLISHED' && version.semver !== semver)
    .sort((a, b) => (b.publishedAt ?? '').localeCompare(a.publishedAt ?? ''))[0]?.semver;
}

/**
 * What doing `action` to `version` would do. `rubric` and `settings` are what the page managed to load;
 * with either missing the plan says less, and never claims what it cannot know.
 */
export function planAction(
  action: RubricAction,
  rubricId: string,
  version: PlannedVersion,
  rubric: IRubricDetail | null,
  settings: IRubricSettings | null,
  enabledCheckCount?: number): RubricActionPlan {

  const plan: RubricActionPlan = {
    action,
    heading: `${ACTION_NAMES[action]} version ${version.semver}?`,
    confirmLabel: ACTION_NAMES[action],
    consequences: [],
    warnings: [],
    blocked: null,
    needsDryRun: false,
    typeToConfirm: null
  };
  const use = pipelineUse(settings, rubricId);
  const active = rubric?.latestPublishedSemver;

  switch (action) {
    case 'publish': {
      if (version.status !== 'DRAFT') {
        plan.blocked = 'Only a draft can be published.';
        return plan;
      }
      plan.consequences.push(`${version.semver} becomes the active version of this rubric: the one validation uses.`);
      if (active) {
        plan.consequences.push(`${active} stays published but is no longer used.`);
      }
      plan.consequences.push('A published version can never be edited. To change it later, create a new version from it.');
      if (use === 'decides') {
        plan.warnings.push('The reporting pipeline validates patient data against this rubric. '
          + 'Once published, this version decides whether that data passes.');
      } else if (use === 'compared') {
        plan.warnings.push('The reporting pipeline runs this rubric beside the validator that decides. '
          + 'Once published, this version is the one compared.');
      }
      if (enabledCheckCount === 0) {
        plan.warnings.push('None of its checks is enabled, so it will find nothing.');
      }
      if (!version.dryRunCompletedAt) {
        if (settings?.dryRunRequiredForPublish) {
          plan.blocked = 'This environment requires a dry run before publishing, and this draft has not had one '
            + 'since it was last saved.';
          plan.needsDryRun = true;
        } else if (settings) {
          plan.warnings.push('This draft has not been dry-run since it was last saved. It can still be published.');
        } else {
          plan.warnings.push('This draft has not been dry-run since it was last saved. '
            + 'If this environment requires a dry run, publishing will be refused.');
        }
      }
      return plan;
    }

    case 'retire': {
      if (version.status !== 'PUBLISHED') {
        plan.blocked = version.status === 'DRAFT'
          ? 'A draft is deleted, not retired.'
          : 'This version is already retired.';
        return plan;
      }
      const next = activeAfterRetiring(rubric, version.semver);
      const noneLeft = !!rubric && !next;
      // The service refuses this one: the pipeline would have no version to validate against.
      if (use === 'decides' && noneLeft) {
        plan.blocked = 'The reporting pipeline validates patient data against this rubric, and this is its only '
          + 'published version. Publish another version before retiring this one.';
        return plan;
      }
      if (!rubric) {
        plan.consequences.push('Validation stops using this version.');
      } else if (version.semver !== active) {
        plan.consequences.push(active
          ? `${active} stays the active version.`
          : 'Validation stops using this version.');
      } else if (next) {
        plan.consequences.push(`${next} becomes the active version again.`);
      } else {
        plan.consequences.push('No published version will remain, so validation against this rubric has nothing to run.');
      }
      plan.consequences.push('A retired version cannot be published again. To bring it back, create a new version from it.');
      if (use === 'decides' && !rubric) {
        // With the rubric unread the page cannot tell whether a version would remain; the service can.
        plan.warnings.push('The reporting pipeline validates patient data against this rubric. '
          + 'If this is its only published version, retiring it will be refused.');
      } else if (use === 'compared' && (noneLeft || !rubric)) {
        // Nothing stops this one: the comparison stops, but the validator that decides carries on.
        plan.warnings.push(noneLeft
          ? 'The reporting pipeline compares this rubric with the validator that decides. With no published version, that comparison has nothing to run.'
          : 'The reporting pipeline compares this rubric with the validator that decides. If no other version is published, that comparison has nothing to run.');
        plan.typeToConfirm = rubricId;
      }
      return plan;
    }

    case 'delete': {
      plan.heading = `Delete draft ${version.semver}?`;
      if (version.status !== 'DRAFT') {
        plan.blocked = 'Only a draft can be deleted. A published version is retired.';
        return plan;
      }
      plan.consequences.push('The draft and its checks are removed. This cannot be undone.');
      if (rubric && (rubric.versions ?? []).length <= 1) {
        plan.consequences.push('It is the rubric\'s only version, so the rubric is removed with it, history included.');
      } else {
        plan.consequences.push('Its version number can be used again, and the deletion is recorded in the rubric\'s history.');
      }
      return plan;
    }
  }
}

/** True when deleting the draft would remove the rubric with it. */
export function deletingRemovesRubric(rubric: IRubricDetail | null): boolean {
  return !!rubric && (rubric.versions ?? []).length <= 1;
}
