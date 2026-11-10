import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';

import {
  IRubricDefinition,
  IRubricDetail,
  IRubricValidationResult,
  IRubricVersionDetail,
  IRubricVersionSummary
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { confirmLeavingRubricEditor } from './rubric-editor-leave.guard';
import { EDITOR_DEFINITION_STATE } from './rubric-editor-state';
import { RubricEditorComponent } from './rubric-editor.component';

const stored: IRubricDefinition = {
  id: 'piqi.core', semver: '1.1.0', title: 'PIQI Core', owner: 'lcg',
  dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
  scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
  checks: [
    { id: 'first', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', ordinal: 1, enabled: true, parameters: { expression: 'Patient.name.exists()' } },
    { id: 'second', type: 'VALUESET', dimension: 'TERMINOLOGY', severityOverride: 'WARNING', ordinal: 2, enabled: true, parameters: { path: 'Patient.gender', valueSet: 'http://vs' } }
  ]
};

function version(status: IRubricVersionDetail['status'] = 'DRAFT', definition = stored): IRubricVersionDetail {
  return { rubricVersionId: 2, rubricId: definition.id, semver: definition.semver, status, checksum: 'sum-1', definition };
}

const rubric: IRubricDetail = {
  rubricId: 'piqi.core', latestPublishedSemver: '1.0.0',
  versions: [
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'DRAFT', checksum: 'sum-1' },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'sum-0' }
  ]
};

function saved(semver: string, checksum = 'sum-2'): IRubricVersionSummary {
  return { rubricVersionId: 3, rubricId: 'piqi.core', semver, status: 'DRAFT', checksum };
}

const valid: IRubricValidationResult = { valid: true, errors: [] };

describe('RubricEditorComponent', () => {
  let fixture: ComponentFixture<RubricEditorComponent>;
  let component: RubricEditorComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let params: BehaviorSubject<ParamMap>;
  let queryParams: Record<string, string>;
  let router: Router;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService',
      ['getRubric', 'getVersion', 'getSettings', 'validate', 'validateText', 'saveDraft', 'deleteDraft']);
    rubricService.getSettings.and.returnValue(of(null));
    rubricService.getRubric.and.returnValue(of(rubric));
    rubricService.getVersion.and.callFake((_id: string, semver: string) =>
      of(version(semver === '1.0.0' ? 'PUBLISHED' : 'DRAFT', { ...stored, semver })));
    rubricService.validate.and.returnValue(of(valid));
    rubricService.saveDraft.and.callFake((definition: IRubricDefinition) => of(saved(definition.semver)));
    rubricService.deleteDraft.and.returnValue(of(undefined));

    params = new BehaviorSubject(convertToParamMap({}));
    queryParams = {};
    window.history.replaceState({}, '');

    await TestBed.configureTestingModule({
      imports: [RubricEditorComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        { provide: ActivatedRoute, useValue: { paramMap: params, snapshot: { get queryParamMap() { return convertToParamMap(queryParams); } } } }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  function open(route: Record<string, string> = {}, query: Record<string, string> = {}): void {
    queryParams = query;
    params.next(convertToParamMap(route));
    fixture = TestBed.createComponent(RubricEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const lastSave = () => rubricService.saveDraft.calls.mostRecent().args;

  // ------------------------------------------------------------------------------------------- opening

  describe('opening', () => {
    it('a new rubric starts blank, with nothing to lose', () => {
      open();

      expect(component.mode).toBe('new-rubric');
      expect(component.idEditable).toBeTrue();
      expect(component.definition.checks).toEqual([]);
      expect(component.dirty).toBeFalse();
      expect(rubricService.getVersion).not.toHaveBeenCalled();
    });

    it('a new version is a copy of the active version, numbered above the newest version of any status', () => {
      open({ rubricId: 'piqi.core' });

      expect(component.mode).toBe('new-version');
      expect(rubricService.getVersion).toHaveBeenCalledOnceWith('piqi.core', '1.0.0');
      expect(component.definition.semver).toBe('1.1.1'); // above the 1.1.0 draft, not the 1.0.0 it copies
      expect(component.definition.checks.length).toBe(2);
      expect(component.idEditable).toBeFalse();
      // nothing is saved under this version yet
      expect(component.dirty).toBeTrue();
    });

    it('a new version copies the version asked for', () => {
      open({ rubricId: 'piqi.core' }, { from: '1.1.0' });

      expect(rubricService.getVersion).toHaveBeenCalledOnceWith('piqi.core', '1.1.0');
    });

    it('a draft opens as saved', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });

      expect(component.mode).toBe('edit');
      expect(component.locked).toBeNull();
      expect(component.dirty).toBeFalse();
      expect(text()).toContain('All changes saved');
    });

    it('a published version cannot be edited, and says how to change the rubric instead', () => {
      open({ rubricId: 'piqi.core', semver: '1.0.0' });

      expect(component.locked).toContain('published');
      expect(text()).toContain('This version cannot be edited.');
      expect(text()).toContain('Create a new version from it');
      expect((fixture.nativeElement as HTMLElement).querySelector('#rubricIdField')).toBeNull();
    });

    it('says so when the version does not exist', () => {
      rubricService.getVersion.and.returnValue(throwError(() => new RubricApiError('not found', 404)));

      open({ rubricId: 'piqi.core', semver: '9.9.9' });

      expect(text()).toContain('The rubric "piqi.core" has no version 9.9.9.');
    });

    it('opens a draft even when the rubric\'s other versions cannot be read', () => {
      rubricService.getRubric.and.returnValue(throwError(() => new RubricApiError('unavailable', 500)));

      open({ rubricId: 'piqi.core', semver: '1.1.0' });

      expect(component.loadError).toBeNull();
      expect(component.definition.id).toBe('piqi.core');
    });

    it('opens a definition handed over by the import, unsaved, in place of a blank one', () => {
      window.history.replaceState({ [EDITOR_DEFINITION_STATE]: { ...stored, id: 'brought.in', semver: '3.0.0' } }, '');

      open();

      expect(component.definition.id).toBe('brought.in');
      expect(component.definition.semver).toBe('3.0.0');
      expect(component.dirty).toBeTrue();
      // read once: a reload does not bring it back
      expect(window.history.state[EDITOR_DEFINITION_STATE]).toBeUndefined();
    });
  });

  // ------------------------------------------------------------------------------------------- editing

  describe('editing', () => {
    beforeEach(() => open({ rubricId: 'piqi.core', semver: '1.1.0' }));

    it('knows there are unsaved changes, and when a change is undone', () => {
      component.definition.title = 'Changed';
      component.onChanged();
      expect(component.dirty).toBeTrue();

      component.definition.title = 'PIQI Core';
      component.onChanged();
      expect(component.dirty).toBeFalse();
    });

    it('does not let a dimension a check uses be removed, and keeps the dimensions in order', () => {
      expect(component.dimensionInUse('CONFORMANCE')).toBeTrue();
      expect(component.dimensionInUse('CURRENCY')).toBeFalse();

      component.toggleDimension('CURRENCY', true);
      component.toggleDimension('COMPLETENESS', true);

      expect(component.definition.dimensions).toEqual(['CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'CURRENCY']);
    });

    it('drops the roll-up for pass/fail and gives one back for a scorecard', () => {
      component.definition.scoringPolicy.type = 'piqi-pass-fail';
      component.onScoringPolicyChanged();
      expect(component.needsRollup).toBeFalse();
      expect(component.definition.scoringPolicy.rollup).toBeUndefined();

      component.definition.scoringPolicy.type = 'piqi-check-scorecard';
      component.onScoringPolicyChanged();
      expect(component.definition.scoringPolicy.rollup).toBe('worst-of');
    });

    it('steps the version above the rubric\'s newest other version', () => {
      component.setVersion('patch');
      expect(component.definition.semver).toBe('1.0.1');
      component.setVersion('minor');
      expect(component.definition.semver).toBe('1.1.0');
      component.setVersion('major');
      expect(component.definition.semver).toBe('2.0.0');
    });

    it('catches a version number another version of the rubric already has', () => {
      component.definition.semver = '1.0.0';
      component.onChanged();
      component.validate();

      expect(component.errors('semver').map(issue => issue.message)).toEqual([
        'Version 1.0.0 of this rubric already exists. Choose another version number.']);
      expect(rubricService.validate).not.toHaveBeenCalled();
    });

    it('adds a resource type or a tag once, and removes it', () => {
      component.newResourceType = ' Patient ';
      component.addResourceType();
      component.newResourceType = 'Patient';
      component.addResourceType();
      component.newWorkflowTag = 'nhsn-dqm';
      component.addWorkflowTag();

      expect(component.fhirResources).toEqual(['Patient']);
      expect(component.workflowTags).toEqual(['nhsn-dqm']);
      expect(component.newResourceType).toBe('');

      component.removeFromContext('fhirResources', 'Patient');
      expect(component.fhirResources).toEqual([]);
    });

    describe('checks', () => {
      it('adds a check at the end once its form is applied', () => {
        component.newCheckType = 'COMPLETENESS';
        component.addCheck();
        expect(component.editing).toEqual(jasmine.objectContaining({ index: 2, adding: true }));
        expect(component.editing!.check.dimension).toBe('COMPLETENESS');

        component.applyCheck({ ...component.editing!.check, id: 'third', parameters: { expression: 'true' } });

        expect(component.definition.checks.map(check => check.id)).toEqual(['first', 'second', 'third']);
        expect(component.editing).toBeNull();
        expect(component.dirty).toBeTrue();
      });

      it('leaves the draft alone when a check\'s form is cancelled', () => {
        component.addCheck();
        component.cancelCheck();

        expect(component.definition.checks.length).toBe(2);
        expect(component.dirty).toBeFalse();
      });

      it('replaces a check when its form is applied', () => {
        component.editCheck(0);
        component.applyCheck({ ...component.definition.checks[0], severityOverride: 'INFORMATION' });

        expect(component.definition.checks[0].severityOverride).toBe('INFORMATION');
        expect(component.definition.checks.length).toBe(2);
      });

      it('moves a check up and down, and not past either end', () => {
        component.moveCheck(0, 1);
        expect(component.definition.checks.map(check => check.id)).toEqual(['second', 'first']);
        component.moveCheck(0, -1);
        component.moveCheck(1, 1);
        expect(component.definition.checks.map(check => check.id)).toEqual(['second', 'first']);
      });

      it('duplicates a check next to the original, with an id that is not taken', () => {
        component.duplicateCheck(0);
        component.duplicateCheck(0);

        expect(component.definition.checks.map(check => check.id)).toEqual(['first', 'first-copy-2', 'first-copy', 'second']);
      });

      it('deletes a check, and enables or disables one', () => {
        component.setCheckEnabled(1, false);
        expect(component.definition.checks[1].enabled).toBeFalse();

        component.deleteCheck(0);
        expect(component.definition.checks.map(check => check.id)).toEqual(['second']);
      });
    });
  });

  // ------------------------------------------------------------------------------------------- validating

  describe('Validate', () => {
    beforeEach(() => open({ rubricId: 'piqi.core', semver: '1.1.0' }));

    it('shows the form\'s own problems without asking the service', () => {
      component.definition.checks = [];
      component.onChanged();

      component.validate();
      fixture.detectChanges();

      expect(rubricService.validate).not.toHaveBeenCalled();
      expect(component.selectedTab).toBe(1);
      expect(text()).toContain('Add at least one check.');
    });

    it('asks the service when the form finds nothing, and says so when it finds nothing either', () => {
      component.validate();

      expect(rubricService.validate).toHaveBeenCalledTimes(1);
      expect(component.notice).toBe('No problems found. The draft is ready to save.');
      expect(rubricService.saveDraft).not.toHaveBeenCalled();
    });

    it('places what the service finds on the check it is about', () => {
      rubricService.validate.and.returnValue(of({
        valid: false, errors: ['checks[first]: invalid FHIRPath expression: unexpected token', 'owner: must be at most 128 characters']
      }));

      component.validate();
      fixture.detectChanges();

      expect(component.checkIssueCount(0)).toBe(1);
      expect(component.serverIssuesForCheck(0)[0].field).toBe('parameters.expression');
      expect(component.errors('owner').length).toBe(1);
      expect(text()).toContain('There are 2 problems to fix');
      expect(text()).toContain('Check first');
    });

    it('drops what the service said once the draft is changed', () => {
      rubricService.validate.and.returnValue(of({ valid: false, errors: ['owner: too long'] }));
      component.validate();

      component.definition.owner = 'shorter';
      component.onChanged();

      expect(component.errors('owner')).toEqual([]);
    });

    it('opens the check a problem in the summary is about', () => {
      rubricService.validate.and.returnValue(of({ valid: false, errors: ['checks[second]: VALUESET requires parameters.path'] }));
      component.validate();

      component.goToIssue(component.issues[0]);

      expect(component.selectedTab).toBe(1);
      expect(component.editing?.index).toBe(1);
    });

    it('says why when the service could not be asked', () => {
      rubricService.validate.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));

      component.validate();

      expect(component.failure).toBe('Service unavailable');
    });
  });

  // ------------------------------------------------------------------------------------------- saving

  describe('Save draft', () => {
    it('does not save a draft the form finds problems with', () => {
      open();

      component.save();

      expect(rubricService.saveDraft).not.toHaveBeenCalled();
      expect(component.errors('id').length).toBe(1);
    });

    it('a new version must not exist yet, and its address becomes the saved draft\'s', () => {
      open({ rubricId: 'piqi.core' });

      component.save();

      const [definition, precondition] = lastSave();
      expect(definition.semver).toBe('1.1.1');
      expect(definition.checks.map(check => check.ordinal)).toEqual([1, 2]);
      expect(precondition).toEqual({ mustNotExist: true });
      expect(component.mode).toBe('edit');
      expect(component.dirty).toBeFalse();
      expect(router.navigate).toHaveBeenCalledWith(
        ['/rubrics', 'piqi.core', 'versions', '1.1.1', 'edit'], jasmine.objectContaining({ replaceUrl: true }));
    });

    it('a draft is saved against the checksum it was opened with, then against the one the save returned', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.title = 'Changed';
      component.onChanged();

      component.save();
      expect(lastSave()[1]).toEqual({ expectedChecksum: 'sum-1' });
      expect(component.notice).toBe('Draft saved.');

      component.definition.title = 'Changed again';
      component.onChanged();
      component.save();
      expect(lastSave()[1]).toEqual({ expectedChecksum: 'sum-2' });
    });

    it('changing a saved draft\'s version saves it as a new version, then deletes the old one', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.semver = '2.0.0';
      component.onChanged();

      component.save();

      expect(lastSave()[1]).toEqual({ mustNotExist: true });
      expect(rubricService.deleteDraft).toHaveBeenCalledOnceWith('piqi.core', '1.1.0');
      expect(component.notice).toBe('Draft saved as version 2.0.0; the draft under 1.1.0 was deleted.');
    });

    it('says so when the old draft could not be deleted after a move; the draft is still saved', () => {
      rubricService.deleteDraft.and.returnValue(throwError(() => new RubricApiError('no', 500)));
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.semver = '2.0.0';
      component.onChanged();

      component.save();

      expect(component.dirty).toBeFalse();
      expect(component.notice).toContain('The draft under 1.1.0 could not be deleted');
    });

    it('a draft someone else changed is not saved; it can be reloaded or saved anyway', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.title = 'Mine';
      component.onChanged();
      rubricService.saveDraft.and.returnValue(throwError(() => new RubricApiError('changed', 412)));

      component.save();
      fixture.detectChanges();

      expect(component.staleDraft).toBeTrue();
      expect(component.dirty).toBeTrue();
      expect(text()).toContain('changed by someone else');

      rubricService.saveDraft.and.callFake((definition: IRubricDefinition) => of(saved(definition.semver)));
      component.save(true);

      // saved with no precondition: it replaces whatever is there
      expect(lastSave()[1]).toEqual({});
      expect(component.staleDraft).toBeFalse();
      expect(component.dirty).toBeFalse();
    });

    it('reloading after a refused save takes the other person\'s version and drops the changes made here', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.title = 'Mine';
      component.onChanged();
      rubricService.getVersion.and.returnValue(of({ ...version('DRAFT', { ...stored, title: 'Theirs' }), checksum: 'sum-9' }));

      component.reload();

      expect(component.definition.title).toBe('Theirs');
      expect(component.dirty).toBeFalse();
      component.definition.owner = 'changed';
      component.onChanged();
      component.save();
      expect(lastSave()[1]).toEqual({ expectedChecksum: 'sum-9' });
    });

    it('a new version whose number was taken meanwhile is a problem with the version, not a lost save', () => {
      open({ rubricId: 'piqi.core' });
      rubricService.saveDraft.and.returnValue(throwError(() => new RubricApiError('exists', 412)));

      component.save();

      expect(component.staleDraft).toBeFalse();
      expect(component.errors('semver')[0].message).toContain('already exists');
    });

    it('a version that is published or retired is a problem with the version', () => {
      open({ rubricId: 'piqi.core' });
      rubricService.saveDraft.and.returnValue(throwError(() => new RubricApiError('immutable', 409)));

      component.save();

      expect(component.errors('semver')[0].message).toContain('published or retired');
    });

    it('places the problems the service refuses a save with', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.definition.title = 'Changed';
      component.onChanged();
      rubricService.saveDraft.and.returnValue(throwError(() =>
        new RubricApiError('Invalid rubric definition', 400, ['checks[second].parameters.valueSet: must be a canonical URL'])));

      component.save();

      expect(component.checkIssueCount(1)).toBe(1);
      expect(component.dirty).toBeTrue();
    });

    it('does not start a second save while one is under way', () => {
      open({ rubricId: 'piqi.core' });
      rubricService.saveDraft.and.returnValue(new Subject<IRubricVersionSummary>());

      component.save();
      component.save();
      component.validate();

      expect(rubricService.saveDraft).toHaveBeenCalledTimes(1);
      expect(rubricService.validate).not.toHaveBeenCalled();
    });
  });

  // ------------------------------------------------------------------------------------------- pasting

  describe('pasting a definition', () => {
    beforeEach(() => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      component.pasteOpen = true;
    });

    it('replaces the draft with what the service read, unsaved', () => {
      rubricService.validateText.and.returnValue(of({ valid: true, errors: [], definition: { ...stored, title: 'Pasted' } }));
      component.pasteText = 'id: piqi.core';

      component.loadPasted();

      expect(rubricService.validateText).toHaveBeenCalledOnceWith('id: piqi.core', 'yaml');
      expect(component.definition.title).toBe('Pasted');
      expect(component.dirty).toBeTrue();
      expect(component.pasteOpen).toBeFalse();
      expect(rubricService.saveDraft).not.toHaveBeenCalled();
    });

    it('sends text that starts with a brace as JSON', () => {
      rubricService.validateText.and.returnValue(of({ valid: true, errors: [], definition: stored }));
      component.pasteText = '  {"id": "piqi.core"}';

      component.loadPasted();

      expect(rubricService.validateText.calls.mostRecent().args[1]).toBe('json');
    });

    it('still loads a definition that has problems, and shows them where they belong', () => {
      rubricService.validateText.and.returnValue(of({
        valid: false, errors: ['checks[first]: invalid FHIRPath expression: nope'], definition: stored
      }));
      component.pasteText = 'id: piqi.core';

      component.loadPasted();

      expect(component.checkIssueCount(0)).toBe(1);
    });

    it('refuses a definition for another rubric, and leaves the draft as it was', () => {
      rubricService.validateText.and.returnValue(of({ valid: true, errors: [], definition: { ...stored, id: 'other.rubric' } }));
      component.pasteText = 'id: other.rubric';

      component.loadPasted();

      expect(component.pasteErrors[0]).toContain('is for the rubric "other.rubric"');
      expect(component.definition.id).toBe('piqi.core');
      expect(component.dirty).toBeFalse();
    });

    it('says why text that is not a definition could not be read', () => {
      rubricService.validateText.and.returnValue(of({ valid: false, errors: ['Malformed YAML rubric payload: bad indent'] }));
      component.pasteText = 'nonsense: [';

      component.loadPasted();

      expect(component.pasteErrors).toEqual(['Malformed YAML rubric payload: bad indent']);
      expect(component.pasteOpen).toBeTrue();
    });
  });

  // ------------------------------------------------------------------------------------------- leaving

  describe('leaving', () => {
    const leave = (editor: { dirty: boolean }) => TestBed.runInInjectionContext(
      () => confirmLeavingRubricEditor(editor, null as never, null as never, null as never));

    it('is not asked about when nothing is unsaved', () => {
      const confirm = spyOn(window, 'confirm');

      expect(leave({ dirty: false })).toBeTrue();
      expect(confirm).not.toHaveBeenCalled();
    });

    it('is asked about when there are unsaved changes, and the answer decides', () => {
      const confirm = spyOn(window, 'confirm').and.returnValues(false, true);

      expect(leave({ dirty: true })).toBeFalse();
      expect(leave({ dirty: true })).toBeTrue();
      expect(confirm).toHaveBeenCalledTimes(2);
    });

    it('asks the browser to confirm closing the page only when there are unsaved changes', () => {
      open({ rubricId: 'piqi.core', semver: '1.1.0' });
      const clean = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
      component.onBeforeUnload(clean);
      expect(clean.defaultPrevented).toBeFalse();

      component.definition.title = 'Changed';
      component.onChanged();
      const unsaved = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
      component.onBeforeUnload(unsaved);
      expect(unsaved.defaultPrevented).toBeTrue();
    });
  });
});
