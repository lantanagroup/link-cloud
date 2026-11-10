import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';

import {
  IRubricDetail,
  IRubricSettings,
  IRubricVersionDetail,
  IRubricVersionSummary
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { RubricVersionViewComponent } from './rubric-version-view.component';

const version: IRubricVersionDetail = {
  rubricVersionId: 2,
  rubricId: 'piqi.core',
  semver: '1.1.0',
  status: 'PUBLISHED',
  checksum: 'abc123',
  definition: {
    id: 'piqi.core',
    semver: '1.1.0',
    title: 'PIQI Core',
    dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    checks: [
      { id: 'second', type: 'VALUESET', dimension: 'TERMINOLOGY', severityOverride: 'WARNING', ordinal: 2, enabled: false,
        parameters: { path: 'Patient.gender', valueSet: 'http://vs' } },
      { id: 'first', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', ordinal: 1,
        parameters: { expression: 'Patient.name.exists()', failureMessage: 'No name', code: '' } },
      { id: 'combined', type: 'VALUESET', dimension: 'TERMINOLOGY', severityOverride: 'ERROR', ordinal: 3,
        parameters: { matchMode: 'ALL', checks: [{ id: 'child', type: 'FHIRPATH', parameters: { expression: 'true' } }] } }
    ]
  }
};

const draft: IRubricVersionDetail = {
  ...version, rubricVersionId: 3, semver: '1.2.0', status: 'DRAFT', checksum: 'def456',
  definition: { ...version.definition!, semver: '1.2.0' }
};

const rubric: IRubricDetail = {
  rubricId: 'piqi.core',
  latestPublishedSemver: '1.1.0',
  versions: [
    { rubricVersionId: 3, rubricId: 'piqi.core', semver: '1.2.0', status: 'DRAFT', checksum: 'def456' },
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'abc123', publishedAt: '2026-09-03T00:00:00Z' },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'aaa', publishedAt: '2026-09-01T00:00:00Z' }
  ]
};

const summary = (semver: string): IRubricVersionSummary =>
  ({ rubricVersionId: 9, rubricId: 'piqi.core', semver, status: 'PUBLISHED', checksum: 'x' });

describe('RubricVersionViewComponent', () => {
  let fixture: ComponentFixture<RubricVersionViewComponent>;
  let component: RubricVersionViewComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let params: BehaviorSubject<ParamMap>;
  let query: BehaviorSubject<ParamMap>;
  let router: Router;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService',
      ['getVersion', 'getRubric', 'getSettings', 'exportDefinition', 'publish', 'retire', 'deleteDraft']);
    rubricService.getVersion.and.returnValue(of(version));
    rubricService.getRubric.and.returnValue(of(rubric));
    rubricService.getSettings.and.returnValue(of(null));
    params = new BehaviorSubject(convertToParamMap({ rubricId: 'piqi.core', semver: '1.1.0' }));
    query = new BehaviorSubject(convertToParamMap({}));

    await TestBed.configureTestingModule({
      imports: [RubricVersionViewComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        { provide: ActivatedRoute, useValue: { paramMap: params, queryParamMap: query } }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(RubricVersionViewComponent);
    component = fixture.componentInstance;
  });

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('loads the version in the route and lists its checks in the order they run', () => {
    fixture.detectChanges();

    expect(rubricService.getVersion).toHaveBeenCalledOnceWith('piqi.core', '1.1.0');
    expect(component.checks.map(check => check.id)).toEqual(['first', 'second', 'combined']);
    expect(component.enabledCheckCount).toBe(2);
    expect(text()).toContain('PIQI Core · 1.1.0');
    expect(text()).toContain('abc123');
  });

  it('shows the definition as the JSON it was registered with', () => {
    fixture.detectChanges();

    expect(JSON.parse(component.definitionJson)).toEqual(version.definition);
  });

  it('opens one check at a time, and closes it when selected again', () => {
    fixture.detectChanges();
    const [first, second] = component.checks;

    component.toggleCheck(first);
    expect(component.expandedCheckId).toBe('first');
    component.toggleCheck(second);
    expect(component.expandedCheckId).toBe('second');
    component.toggleCheck(second);
    expect(component.expandedCheckId).toBeNull();
  });

  it('lists a check\'s settings with their labels, leaving out the empty ones', () => {
    fixture.detectChanges();

    expect(component.settingsOf(component.checks[0])).toEqual([
      { key: 'expression', label: 'Expression', value: 'Patient.name.exists()' },
      { key: 'failureMessage', label: 'Failure message', value: 'No name' }
    ]);
  });

  it('lists the children of a combined check apart from its settings', () => {
    fixture.detectChanges();
    const combined = component.checks[2];

    expect(component.settingsOf(combined)).toEqual([{ key: 'matchMode', label: 'Passes when', value: 'All of its checks pass' }]);
    expect(component.childrenOf(combined).map(child => child.id)).toEqual(['child']);
    expect(component.childrenOf(component.checks[0])).toEqual([]);
  });

  it('shows a setting it has no label for under its own name', () => {
    expect(component.settingsOf({ id: 'c', type: 'CUSTOM', parameters: { somethingNew: 5, profiles: ['a', 'b'], validateCodings: true } }))
      .toEqual([
        { key: 'somethingNew', label: 'somethingNew', value: '5' },
        { key: 'profiles', label: 'Profiles', value: 'a\nb' },
        { key: 'validateCodings', label: 'Validate codings', value: 'Yes' }
      ]);
  });

  describe('export', () => {
    let link: HTMLAnchorElement;
    let click: jasmine.Spy;
    let createUrl: jasmine.Spy;

    beforeEach(() => {
      fixture.detectChanges();
      link = document.createElement('a');
      click = spyOn(link, 'click');
      const createElement = document.createElement.bind(document);
      spyOn(document, 'createElement').and.callFake((tag: string) => tag === 'a' ? link : createElement(tag));
      createUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:definition');
      spyOn(URL, 'revokeObjectURL');
    });

    it('asks the service for the file and downloads it under the rubric and version name', () => {
      rubricService.exportDefinition.and.returnValue(of('id: piqi.core\n'));

      component.export('yaml');

      expect(rubricService.exportDefinition).toHaveBeenCalledOnceWith('piqi.core', '1.1.0', 'yaml');
      expect(link.download).toBe('piqi.core-1.1.0.yaml');
      expect((createUrl.calls.mostRecent().args[0] as Blob).type).toBe('application/yaml');
      expect(click).toHaveBeenCalled();
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:definition');
      expect(component.exporting).toBeNull();
    });

    it('names a JSON export .json', () => {
      rubricService.exportDefinition.and.returnValue(of('{}'));

      component.export('json');

      expect(link.download).toBe('piqi.core-1.1.0.json');
      expect((createUrl.calls.mostRecent().args[0] as Blob).type).toBe('application/json');
    });

    it('does not start a second export while one is being fetched', () => {
      const pending = new Subject<string>();
      rubricService.exportDefinition.and.returnValue(pending);

      component.export('yaml');
      component.export('json');

      expect(component.exporting).toBe('yaml');
      expect(rubricService.exportDefinition).toHaveBeenCalledTimes(1);
    });

    it('says why an export failed, downloads nothing, and can be tried again', () => {
      rubricService.exportDefinition.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));

      component.export('yaml');
      fixture.detectChanges();

      expect(click).not.toHaveBeenCalled();
      expect(component.exporting).toBeNull();
      expect(text()).toContain('The definition could not be exported.');
      expect(text()).toContain('Service unavailable');

      rubricService.exportDefinition.and.returnValue(of('id: piqi.core\n'));
      component.export('yaml');
      fixture.detectChanges();

      expect(click).toHaveBeenCalled();
      expect(text()).not.toContain('The definition could not be exported.');
    });
  });

  it('says the version does not exist on a 404', () => {
    rubricService.getVersion.and.returnValue(throwError(() => new RubricApiError('Rubric version not found', 404)));

    fixture.detectChanges();

    expect(component.notFound).toBeTrue();
    expect(text()).toContain('The rubric "piqi.core" has no version 1.1.0.');
  });

  it('shows why the version could not be loaded on any other failure', () => {
    rubricService.getVersion.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));

    fixture.detectChanges();

    expect(text()).toContain('The version could not be loaded.');
    expect(text()).toContain('Service unavailable');
  });

  it('says so when a version has no readable definition', () => {
    rubricService.getVersion.and.returnValue(of({ ...version, definition: undefined }));

    fixture.detectChanges();

    expect(component.checks).toEqual([]);
    expect(text()).toContain('This version\'s definition could not be read.');
  });

  it('loads again when the route changes to another version', () => {
    fixture.detectChanges();
    component.toggleCheck(component.checks[0]);

    params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '2.0.0' }));
    fixture.detectChanges();

    expect(rubricService.getVersion.calls.mostRecent().args).toEqual(['piqi.core', '2.0.0']);
    expect(component.expandedCheckId).toBeNull();
  });

  it('shows the version even when the rubric it belongs to could not be read', () => {
    rubricService.getRubric.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));

    fixture.detectChanges();

    expect(component.version).toEqual(version);
    expect(component.rubric).toBeNull();
    expect(component.compareWith).toBeNull();
  });

  describe('what it offers', () => {
    it('marks the version validation uses as active', () => {
      fixture.detectChanges();

      expect(component.isActive).toBeTrue();
      expect(text()).toContain('Active');
    });

    it('offers to publish or delete a draft, and to retire a published version', () => {
      fixture.detectChanges();
      expect(component.actions).toEqual(['retire']);

      rubricService.getVersion.and.returnValue(of(draft));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.2.0' }));
      fixture.detectChanges();
      expect(component.actions).toEqual(['publish', 'delete']);

      rubricService.getVersion.and.returnValue(of({ ...version, status: 'RETIRED' }));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.1.0' }));
      fixture.detectChanges();
      expect(component.actions).toEqual([]);
      expect(Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
        .some(link => link.textContent?.trim() === 'Dry run')).toBeFalse();
    });

    it('compares a draft with the active version, and the active version with the one before it', () => {
      fixture.detectChanges();
      expect(component.compareWith).toBe('1.0.0');

      rubricService.getVersion.and.returnValue(of(draft));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.2.0' }));
      fixture.detectChanges();
      expect(component.compareWith).toBe('1.1.0');
    });
  });

  describe('publishing a draft', () => {
    beforeEach(() => {
      rubricService.getVersion.and.returnValue(of(draft));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.2.0' }));
    });

    it('says what will happen and does nothing until it is confirmed', () => {
      fixture.detectChanges();

      component.open('publish');
      fixture.detectChanges();

      expect(text()).toContain('Publish version 1.2.0?');
      expect(text()).toContain('1.2.0 becomes the active version of this rubric');
      expect(text()).toContain('1.1.0 stays published but is no longer used.');
      expect(text()).toContain('3 checks, 2 enabled');
      expect(rubricService.publish).not.toHaveBeenCalled();
    });

    it('publishes on confirmation, says so, and reads the version again', () => {
      rubricService.publish.and.returnValue(of(summary('1.2.0')));
      fixture.detectChanges();
      component.open('publish');
      rubricService.getVersion.and.returnValue(of({ ...draft, status: 'PUBLISHED' }));

      component.confirm();
      fixture.detectChanges();

      expect(rubricService.publish).toHaveBeenCalledOnceWith('piqi.core', '1.2.0');
      expect(component.plan).toBeNull();
      expect(component.version?.status).toBe('PUBLISHED');
      expect(text()).toContain('Version 1.2.0 is published and is now the active version.');
    });

    it('leaves the draft as it is when the confirmation is cancelled', () => {
      fixture.detectChanges();
      component.open('publish');

      component.cancelAction();

      expect(component.plan).toBeNull();
      expect(rubricService.publish).not.toHaveBeenCalled();
    });

    it('cannot be confirmed where a dry run is required and there is none, and offers one', () => {
      rubricService.getSettings.and.returnValue(of({ dryRunRequiredForPublish: true } as IRubricSettings));
      fixture.detectChanges();

      component.open('publish');
      fixture.detectChanges();

      expect(component.canConfirm).toBeFalse();
      expect(text()).toContain('This environment requires a dry run before publishing');
      expect(text()).toContain('Run a dry run');
      component.confirm();
      expect(rubricService.publish).not.toHaveBeenCalled();
    });

    it('says why the service refused, and keeps the confirmation open', () => {
      rubricService.publish.and.returnValue(throwError(() => new RubricApiError('A completed dry run is required', 409)));
      fixture.detectChanges();
      component.open('publish');

      component.confirm();
      fixture.detectChanges();

      expect(component.plan).not.toBeNull();
      expect(component.acting).toBeFalse();
      expect(text()).toContain('A completed dry run is required');
    });

    it('opens the confirmation another page asked for, and takes the request out of the address', () => {
      query.next(convertToParamMap({ do: 'publish' }));
      fixture.detectChanges();

      expect(component.plan?.action).toBe('publish');

      component.cancelAction();

      expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { do: null }, replaceUrl: true }));
    });

    it('ignores a request the version\'s status does not allow', () => {
      query.next(convertToParamMap({ do: 'retire' }));
      fixture.detectChanges();

      expect(component.plan).toBeNull();
    });
  });

  describe('retiring', () => {
    it('says which version becomes active again', () => {
      fixture.detectChanges();

      component.open('retire');
      fixture.detectChanges();

      expect(text()).toContain('1.0.0 becomes the active version again.');
      expect(component.canConfirm).toBeTrue();
    });

    it('asks for the rubric id when the pipeline uses the rubric and no published version would remain', () => {
      rubricService.getSettings.and.returnValue(of(
        { pipelineRubricId: 'piqi.core', pipelineEnabled: true, pipelineShadowEnabled: false } as IRubricSettings));
      rubricService.getRubric.and.returnValue(of({ ...rubric, versions: rubric.versions!.slice(0, 2) }));
      rubricService.retire.and.returnValue(of(summary('1.1.0')));
      fixture.detectChanges();

      component.open('retire');
      fixture.detectChanges();

      expect(text()).toContain('pipeline validation has nothing to run');
      expect(component.canConfirm).toBeFalse();
      component.confirm();
      expect(rubricService.retire).not.toHaveBeenCalled();

      component.confirmText = 'piqi.core';
      expect(component.canConfirm).toBeTrue();
      component.confirm();
      expect(rubricService.retire).toHaveBeenCalledOnceWith('piqi.core', '1.1.0');
    });
  });

  describe('deleting a draft', () => {
    beforeEach(() => {
      rubricService.getVersion.and.returnValue(of(draft));
      rubricService.deleteDraft.and.returnValue(of(undefined));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.2.0' }));
    });

    it('deletes on confirmation and goes back to the rubric', () => {
      fixture.detectChanges();
      component.open('delete');

      component.confirm();

      expect(rubricService.deleteDraft).toHaveBeenCalledOnceWith('piqi.core', '1.2.0');
      expect(router.navigate).toHaveBeenCalledWith(['/rubrics', 'piqi.core']);
    });

    it('goes back to the list when the draft was the rubric\'s only version', () => {
      rubricService.getRubric.and.returnValue(of({ ...rubric, latestPublishedSemver: undefined, versions: rubric.versions!.slice(0, 1) }));
      fixture.detectChanges();
      component.open('delete');
      fixture.detectChanges();

      expect(text()).toContain('the rubric is removed with it');

      component.confirm();

      expect(router.navigate).toHaveBeenCalledWith(['/rubrics']);
    });
  });
});
