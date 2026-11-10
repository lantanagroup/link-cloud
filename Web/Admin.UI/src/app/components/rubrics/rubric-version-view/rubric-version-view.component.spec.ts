import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';

import { IRubricVersionDetail } from '../../../interfaces/validation/rubric.interface';
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

describe('RubricVersionViewComponent', () => {
  let fixture: ComponentFixture<RubricVersionViewComponent>;
  let component: RubricVersionViewComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let params: BehaviorSubject<ParamMap>;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['getVersion', 'exportDefinition']);
    rubricService.getVersion.and.returnValue(of(version));
    params = new BehaviorSubject(convertToParamMap({ rubricId: 'piqi.core', semver: '1.1.0' }));

    await TestBed.configureTestingModule({
      imports: [RubricVersionViewComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        { provide: ActivatedRoute, useValue: { paramMap: params } }
      ]
    }).compileComponents();

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
      { label: 'Expression', value: 'Patient.name.exists()' },
      { label: 'Failure message', value: 'No name' }
    ]);
  });

  it('lists the children of a combined check apart from its settings', () => {
    fixture.detectChanges();
    const combined = component.checks[2];

    expect(component.settingsOf(combined)).toEqual([{ label: 'Passes when', value: 'All of its checks pass' }]);
    expect(component.childrenOf(combined).map(child => child.id)).toEqual(['child']);
    expect(component.childrenOf(component.checks[0])).toEqual([]);
  });

  it('shows a setting it has no label for under its own name', () => {
    expect(component.settingsOf({ id: 'c', type: 'CUSTOM', parameters: { somethingNew: 5, profiles: ['a', 'b'], validateCodings: true } }))
      .toEqual([
        { label: 'somethingNew', value: '5' },
        { label: 'Profiles', value: 'a\nb' },
        { label: 'Validate codings', value: 'Yes' }
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
});
