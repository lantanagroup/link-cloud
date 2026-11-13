import { IRubricDetail, IRubricSettings } from '../../interfaces/validation/rubric.interface';
import { actionsFor, activeAfterRetiring, deletingRemovesRubric, planAction, PlannedVersion } from './rubric-lifecycle';

const rubric: IRubricDetail = {
  rubricId: 'piqi.core',
  latestPublishedSemver: '1.1.0',
  versions: [
    { rubricVersionId: 3, rubricId: 'piqi.core', semver: '1.2.0', status: 'DRAFT', checksum: 'c' },
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'b', publishedAt: '2026-09-03T00:00:00Z' },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'a', publishedAt: '2026-09-01T00:00:00Z' },
    { rubricVersionId: 0, rubricId: 'piqi.core', semver: '0.9.0', status: 'RETIRED', checksum: 'z', publishedAt: '2026-08-01T00:00:00Z' }
  ]
};

const draft: PlannedVersion = { semver: '1.2.0', status: 'DRAFT' };
const dryRunDraft: PlannedVersion = { ...draft, dryRunCompletedAt: '2026-09-08T00:00:00Z', dryRunStatus: 'UNACCEPTABLE' };
const active: PlannedVersion = { semver: '1.1.0', status: 'PUBLISHED' };

function settings(overrides: Partial<IRubricSettings> = {}): IRubricSettings {
  return { dryRunRequiredForPublish: false, pipelineEnabled: false, pipelineShadowEnabled: false, ...overrides } as IRubricSettings;
}

describe('rubric lifecycle', () => {
  it('offers publish and delete for a draft, retire for a published version, nothing for a retired one', () => {
    expect(actionsFor('DRAFT')).toEqual(['publish', 'delete']);
    expect(actionsFor('PUBLISHED')).toEqual(['retire']);
    expect(actionsFor('RETIRED')).toEqual([]);
    expect(actionsFor(undefined)).toEqual([]);
  });

  describe('publishing', () => {
    it('says the draft becomes active and the active version steps back', () => {
      const plan = planAction('publish', 'piqi.core', dryRunDraft, rubric, settings());

      expect(plan.heading).toBe('Publish version 1.2.0?');
      expect(plan.blocked).toBeNull();
      expect(plan.consequences[0]).toContain('1.2.0 becomes the active version');
      expect(plan.consequences[1]).toBe('1.1.0 stays published but is no longer used.');
      expect(plan.consequences[2]).toContain('can never be edited');
      expect(plan.warnings).toEqual([]);
    });

    it('does not mention a version stepping back when none is published', () => {
      const plan = planAction('publish', 'piqi.core', dryRunDraft, { ...rubric, latestPublishedSemver: undefined }, settings());

      expect(plan.consequences.some(line => line.includes('no longer used'))).toBeFalse();
    });

    it('is not blocked by a dry run that found the sample unacceptable: it completed', () => {
      const plan = planAction('publish', 'piqi.core', dryRunDraft, rubric, settings({ dryRunRequiredForPublish: true }));

      expect(plan.blocked).toBeNull();
    });

    it('is blocked without a dry run where one is required, and says a dry run is what it needs', () => {
      const plan = planAction('publish', 'piqi.core', draft, rubric, settings({ dryRunRequiredForPublish: true }));

      expect(plan.blocked).toContain('requires a dry run');
      expect(plan.needsDryRun).toBeTrue();
    });

    it('only warns without a dry run where none is required', () => {
      const plan = planAction('publish', 'piqi.core', draft, rubric, settings());

      expect(plan.blocked).toBeNull();
      expect(plan.warnings).toEqual(['This draft has not been dry-run since it was last saved. It can still be published.']);
    });

    it('does not claim to know the requirement when the settings could not be read', () => {
      const plan = planAction('publish', 'piqi.core', draft, rubric, null);

      expect(plan.blocked).toBeNull();
      expect(plan.warnings[0]).toContain('If this environment requires a dry run, publishing will be refused.');
    });

    it('warns that the pipeline will use the version, by how the pipeline uses the rubric', () => {
      const decides = planAction('publish', 'piqi.core', dryRunDraft, rubric,
        settings({ pipelineRubricId: 'piqi.core', pipelineEnabled: true }));
      const compared = planAction('publish', 'piqi.core', dryRunDraft, rubric,
        settings({ pipelineRubricId: 'piqi.core', pipelineShadowEnabled: true }));
      const other = planAction('publish', 'piqi.core', dryRunDraft, rubric,
        settings({ pipelineRubricId: 'another', pipelineEnabled: true }));

      expect(decides.warnings[0]).toContain('decides whether that data passes');
      expect(compared.warnings[0]).toContain('is the one compared');
      expect(other.warnings).toEqual([]);
    });

    it('warns when none of the checks is enabled', () => {
      const plan = planAction('publish', 'piqi.core', dryRunDraft, rubric, settings(), 0);

      expect(plan.warnings).toContain('None of its checks is enabled, so it will find nothing.');
    });

    it('is blocked for a version that is not a draft', () => {
      expect(planAction('publish', 'piqi.core', active, rubric, settings()).blocked).toBe('Only a draft can be published.');
    });
  });

  describe('retiring', () => {
    it('makes the most recently published of the others active again', () => {
      expect(activeAfterRetiring(rubric, '1.1.0')).toBe('1.0.0');
      expect(activeAfterRetiring(rubric, '1.0.0')).toBe('1.1.0');
      expect(activeAfterRetiring({ ...rubric, versions: rubric.versions!.slice(0, 2) }, '1.1.0')).toBeUndefined();
    });

    it('goes by when a version was published, not by its number', () => {
      const hotfix: IRubricDetail = {
        rubricId: 'piqi.core',
        latestPublishedSemver: '1.0.1',
        versions: [
          { rubricVersionId: 3, rubricId: 'piqi.core', semver: '2.0.0', status: 'PUBLISHED', checksum: 'c', publishedAt: '2026-09-01T00:00:00Z' },
          { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.0.1', status: 'PUBLISHED', checksum: 'b', publishedAt: '2026-09-05T00:00:00Z' },
          { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'a', publishedAt: '2026-08-01T00:00:00Z' }
        ]
      };

      expect(activeAfterRetiring(hotfix, '1.0.1')).toBe('2.0.0');
    });

    it('says which version becomes active when the active one is retired', () => {
      const plan = planAction('retire', 'piqi.core', active, rubric, settings());

      expect(plan.consequences[0]).toBe('1.0.0 becomes the active version again.');
      expect(plan.consequences[1]).toContain('cannot be published again');
      expect(plan.typeToConfirm).toBeNull();
    });

    it('says the active version stays when another published version is retired', () => {
      const plan = planAction('retire', 'piqi.core', { semver: '1.0.0', status: 'PUBLISHED' }, rubric, settings());

      expect(plan.consequences[0]).toBe('1.1.0 stays the active version.');
    });

    it('says nothing will be left to run when the last published version is retired', () => {
      const plan = planAction('retire', 'piqi.core', active, { ...rubric, versions: rubric.versions!.slice(0, 2) }, settings());

      expect(plan.consequences[0]).toContain('No published version will remain');
      expect(plan.typeToConfirm).toBeNull();
    });

    it('asks for the rubric id when that rubric is the one the pipeline uses', () => {
      const plan = planAction('retire', 'piqi.core', active, { ...rubric, versions: rubric.versions!.slice(0, 2) },
        settings({ pipelineRubricId: 'piqi.core', pipelineEnabled: true }));

      expect(plan.warnings[0]).toContain('pipeline validation has nothing to run');
      expect(plan.typeToConfirm).toBe('piqi.core');
    });

    it('does not ask for it while another published version remains', () => {
      const plan = planAction('retire', 'piqi.core', active, rubric,
        settings({ pipelineRubricId: 'piqi.core', pipelineEnabled: true }));

      expect(plan.typeToConfirm).toBeNull();
      expect(plan.warnings).toEqual([]);
    });

    it('asks for it when the rubric could not be read and the pipeline uses the rubric: it cannot tell', () => {
      const plan = planAction('retire', 'piqi.core', active, null,
        settings({ pipelineRubricId: 'piqi.core', pipelineEnabled: true }));

      expect(plan.consequences[0]).toBe('Validation stops using this version.');
      expect(plan.typeToConfirm).toBe('piqi.core');
    });

    it('is blocked for a draft, which is deleted, and for a version already retired', () => {
      expect(planAction('retire', 'piqi.core', draft, rubric, settings()).blocked).toBe('A draft is deleted, not retired.');
      expect(planAction('retire', 'piqi.core', { semver: '0.9.0', status: 'RETIRED' }, rubric, settings()).blocked)
        .toBe('This version is already retired.');
    });
  });

  describe('deleting', () => {
    it('says the draft is removed and its number freed', () => {
      const plan = planAction('delete', 'piqi.core', draft, rubric, settings());

      expect(plan.heading).toBe('Delete draft 1.2.0?');
      expect(plan.consequences[0]).toContain('cannot be undone');
      expect(plan.consequences[1]).toContain('version number can be used again');
      expect(deletingRemovesRubric(rubric)).toBeFalse();
    });

    it('says the rubric goes with its only version', () => {
      const only = { ...rubric, versions: rubric.versions!.slice(0, 1) };
      const plan = planAction('delete', 'piqi.core', draft, only, settings());

      expect(plan.consequences[1]).toContain('the rubric is removed with it');
      expect(deletingRemovesRubric(only)).toBeTrue();
    });

    it('is blocked for a version that is not a draft', () => {
      expect(planAction('delete', 'piqi.core', active, rubric, settings()).blocked).toContain('Only a draft can be deleted');
    });
  });
});
