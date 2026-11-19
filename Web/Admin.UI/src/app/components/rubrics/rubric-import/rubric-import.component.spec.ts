import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import {
  IRubricDefinition,
  IRubricValidationResult,
  IRubricVersionSummary
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { EDITOR_DEFINITION_STATE } from '../rubric-editor/rubric-editor-state';
import { RubricImportComponent } from './rubric-import.component';

const definition: IRubricDefinition = {
  id: 'piqi.core', semver: '1.1.0', title: 'PIQI Core', owner: 'lcg',
  dimensions: ['CONFORMANCE'],
  scoringPolicy: { type: 'piqi-pass-fail' },
  checks: [
    { id: 'a', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: true, parameters: { expression: 'true' } },
    { id: 'b', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: false, parameters: { expression: 'false' } }
  ]
};

function result(overrides: Partial<IRubricValidationResult> = {}): IRubricValidationResult {
  return { valid: true, errors: [], definition, checksum: 'new-sum', rubricExists: false, ...overrides };
}

const imported: IRubricVersionSummary = { rubricVersionId: 5, rubricId: 'piqi.core', semver: '1.1.0', status: 'DRAFT', checksum: 'new-sum' };

describe('RubricImportComponent', () => {
  let fixture: ComponentFixture<RubricImportComponent>;
  let component: RubricImportComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let router: Router;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['validateText', 'validate', 'saveDraft', 'getRubric']);
    rubricService.validateText.and.returnValue(of(result()));
    rubricService.saveDraft.and.returnValue(of(imported));

    await TestBed.configureTestingModule({
      imports: [RubricImportComponent],
      providers: [provideRouter([]), { provide: RubricService, useValue: rubricService }]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(RubricImportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  function review(reviewed: IRubricValidationResult, pasted = 'id: piqi.core'): void {
    rubricService.validateText.and.returnValue(of(reviewed));
    component.text = pasted;
    component.onTextChanged();
    component.reviewText();
    fixture.detectChanges();
  }

  describe('reviewing', () => {
    it('sends pasted YAML as YAML and pasted JSON as JSON, and saves nothing', () => {
      review(result(), 'id: piqi.core');
      expect(rubricService.validateText.calls.mostRecent().args).toEqual(['id: piqi.core', 'yaml']);

      review(result(), ' {"id": "piqi.core"} ');
      expect(rubricService.validateText.calls.mostRecent().args).toEqual(['{"id": "piqi.core"}', 'json']);

      expect(rubricService.saveDraft).not.toHaveBeenCalled();
    });

    it('goes by the file\'s name when the text came from a file', async () => {
      const file = new File(['{"id": "piqi.core"}'], 'rubric.JSON');
      await component.onFileChosen({ target: { files: [file], value: 'x' } } as unknown as Event);

      component.reviewText();

      expect(component.fileName).toBe('rubric.JSON');
      expect(rubricService.validateText.calls.mostRecent().args[1]).toBe('json');
    });

    it('refuses a file that is not YAML or JSON', async () => {
      await component.onFileChosen({ target: { files: [new File(['x'], 'rubric.docx')], value: 'x' } } as unknown as Event);

      expect(component.fileError).toBe('"rubric.docx" is not a .yaml, .yml or .json file.');
      expect(component.text).toBe('');
    });

    it('shows what the definition is: rubric, version, checks and how many are enabled', () => {
      review(result());

      expect(text()).toContain('PIQI Core');
      expect(text()).toContain('1.1.0');
      expect(text()).toContain('2 (1 enabled)');
      expect(text()).toContain('Pass/fail');
    });

    it('forgets the review when the text changes: it is no longer about that text', () => {
      review(result());

      component.text = 'id: other';
      component.onTextChanged();

      expect(component.review).toBeNull();
      expect(component.canImport).toBeFalse();
    });
  });

  describe('what importing would do', () => {
    it('creates a rubric that does not exist', () => {
      review(result({ rubricExists: false }));

      expect(component.effect).toBe('new-rubric');
      expect(text()).toContain('Importing creates the rubric');
      expect(component.canImport).toBeTrue();
    });

    it('adds a version to a rubric that exists', () => {
      review(result({ rubricExists: true }));

      expect(component.effect).toBe('new-version');
      expect(text()).toContain('Importing adds version 1.1.0');
    });

    it('replaces a draft whose definition differs, and says so', () => {
      review(result({ rubricExists: true, existingStatus: 'DRAFT', existingChecksum: 'old-sum' }));

      expect(component.effect).toBe('replace-draft');
      expect(text()).toContain('replaces that draft');
      expect(component.canImport).toBeTrue();
    });

    it('has nothing to do for a draft that already has exactly this definition', () => {
      review(result({ rubricExists: true, existingStatus: 'DRAFT', existingChecksum: 'new-sum' }));

      expect(component.effect).toBe('unchanged-draft');
      expect(text()).toContain('There is nothing to import.');
      expect(component.canImport).toBeFalse();
    });

    it('cannot change a published version; offers the next version when the definitions differ', () => {
      review(result({ rubricExists: true, existingStatus: 'PUBLISHED', existingChecksum: 'old-sum' }));

      expect(component.effect).toBe('immutable');
      expect(component.canImport).toBeFalse();
      expect(text()).toContain('is published and cannot be changed');
      expect(text()).toContain('Review it as the next version');
    });

    it('has nothing to do for a retired version that already has exactly this definition', () => {
      review(result({ rubricExists: true, existingStatus: 'RETIRED', existingChecksum: 'new-sum' }));

      expect(text()).toContain('is retired and cannot be changed');
      expect(text()).toContain('nothing to import');
      expect(text()).not.toContain('Review it as the next version');
    });
  });

  describe('a definition with problems', () => {
    it('lists them and cannot be imported, but can be opened in the editor to fix', () => {
      review(result({ valid: false, errors: ['checks[a]: invalid FHIRPath expression: nope', 'owner: too long'], checksum: undefined }));

      expect(component.effect).toBeNull();
      expect(component.canImport).toBeFalse();
      expect(text()).toContain('This definition has 2 problems');
      expect(text()).toContain('checks[a]: invalid FHIRPath expression: nope');
      expect(text()).toContain('Open in the editor');
    });

    it('opens in the editor as a new rubric when the rubric does not exist', () => {
      review(result({ valid: false, errors: ['owner: too long'] }));

      component.openInEditor();

      expect(router.navigate).toHaveBeenCalledWith(['/rubrics', '-', 'new'], { state: { [EDITOR_DEFINITION_STATE]: definition } });
    });

    it('opens in the editor as a new version when the rubric exists', () => {
      review(result({ valid: false, errors: ['owner: too long'], rubricExists: true }));

      component.openInEditor();

      expect(router.navigate).toHaveBeenCalledWith(
        ['/rubrics', 'piqi.core', 'versions', 'new'], { state: { [EDITOR_DEFINITION_STATE]: definition } });
    });

    it('says text that is not a definition could not be read, and offers no editor', () => {
      review({ valid: false, errors: ['Malformed YAML rubric payload: bad indent'] });

      expect(text()).toContain('This could not be read as a rubric definition.');
      expect(text()).toContain('Malformed YAML rubric payload: bad indent');
      expect(text()).not.toContain('Open in the editor');
    });
  });

  describe('importing', () => {
    it('saves a new version only if it still does not exist, then opens it', () => {
      review(result({ rubricExists: true }));

      component.importDefinition();

      expect(rubricService.saveDraft).toHaveBeenCalledOnceWith(definition, { mustNotExist: true });
      expect(router.navigate).toHaveBeenCalledWith(['/rubrics', 'piqi.core', 'versions', '1.1.0']);
    });

    it('replaces a draft only if it is still the draft that was reviewed', () => {
      review(result({ rubricExists: true, existingStatus: 'DRAFT', existingChecksum: 'old-sum' }));

      component.importDefinition();

      expect(rubricService.saveDraft).toHaveBeenCalledOnceWith(definition, { expectedChecksum: 'old-sum' });
    });

    it('does nothing when there is nothing that can be imported', () => {
      review(result({ rubricExists: true, existingStatus: 'PUBLISHED', existingChecksum: 'old-sum' }));

      component.importDefinition();

      expect(rubricService.saveDraft).not.toHaveBeenCalled();
    });

    it('asks for another review when the rubric changed in the meantime', () => {
      review(result());
      rubricService.saveDraft.and.returnValue(throwError(() => new RubricApiError('changed', 412)));

      component.importDefinition();

      expect(component.failure).toContain('changed while you were reviewing it');
      expect(component.review).toBeNull();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('says why the service refused it', () => {
      review(result());
      rubricService.saveDraft.and.returnValue(throwError(() => new RubricApiError('Invalid rubric definition', 400, ['owner: too long'])));

      component.importDefinition();

      expect(component.failure).toBe('Invalid rubric definition: owner: too long');
    });
  });

  it('reviews a definition under the rubric\'s next free version when its own is published', () => {
    review(result({ rubricExists: true, existingStatus: 'PUBLISHED', existingChecksum: 'old-sum' }));
    rubricService.getRubric.and.returnValue(of({
      rubricId: 'piqi.core',
      versions: [
        { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.2.0', status: 'DRAFT', checksum: 'x' },
        { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'old-sum' }
      ]
    }));
    rubricService.validate.and.returnValue(of(result({ rubricExists: true, definition: { ...definition, semver: '1.2.1' } })));

    component.reviewAsNextVersion();

    // above the 1.2.0 draft, not just above the published 1.1.0
    expect(rubricService.validate.calls.mostRecent().args[0].semver).toBe('1.2.1');
    expect(JSON.parse(component.text).semver).toBe('1.2.1');
    expect(component.effect).toBe('new-version');
    expect(component.canImport).toBeTrue();
  });
});
