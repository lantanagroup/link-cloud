import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { IRubricDefinition, IRubricDetail, IRubricVersionDetail } from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { defaultComparison, RubricCompareComponent } from './rubric-compare.component';

const rubric: IRubricDetail = {
  rubricId: 'piqi.core',
  title: 'PIQI Core',
  latestPublishedSemver: '1.1.0',
  versions: [
    { rubricVersionId: 3, rubricId: 'piqi.core', semver: '1.2.0', status: 'DRAFT', checksum: 'c' },
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'b' },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'RETIRED', checksum: 'a' }
  ]
};

const published: IRubricDefinition = {
  id: 'piqi.core', semver: '1.1.0', title: 'PIQI Core', dimensions: ['CONFORMANCE'],
  scoringPolicy: { type: 'piqi-pass-fail' },
  checks: [
    { id: 'name', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', parameters: { expression: 'Patient.name.exists()' } },
    { id: 'old', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', parameters: { expression: 'true' } }
  ]
};

const draft: IRubricDefinition = {
  ...published, semver: '1.2.0', title: 'PIQI Core, revised',
  checks: [
    { id: 'name', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'WARNING', parameters: { expression: 'Patient.name.exists()' } },
    { id: 'birthdate', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', parameters: { expression: 'Patient.birthDate.exists()' } }
  ]
};

function detail(definition: IRubricDefinition | undefined, semver: string): IRubricVersionDetail {
  return { rubricVersionId: 1, rubricId: 'piqi.core', semver, status: 'DRAFT', checksum: 'x', definition };
}

describe('defaultComparison', () => {
  it('compares the newest version with the active one', () => {
    expect(defaultComparison(rubric)).toEqual({ from: '1.1.0', to: '1.2.0' });
  });

  it('compares the newest with the one before it when the newest is the active one, or none is', () => {
    expect(defaultComparison({ ...rubric, latestPublishedSemver: '1.2.0' })).toEqual({ from: '1.1.0', to: '1.2.0' });
    expect(defaultComparison({ ...rubric, latestPublishedSemver: undefined })).toEqual({ from: '1.1.0', to: '1.2.0' });
  });

  it('has nothing to compare for a rubric with one version', () => {
    expect(defaultComparison({ ...rubric, versions: rubric.versions!.slice(0, 1) })).toBeNull();
  });
});

describe('RubricCompareComponent', () => {
  let fixture: ComponentFixture<RubricCompareComponent>;
  let component: RubricCompareComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let query: BehaviorSubject<ParamMap>;
  let router: Router;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['getRubric', 'getVersion']);
    rubricService.getRubric.and.returnValue(of(rubric));
    rubricService.getVersion.and.callFake((_id: string, semver: string) =>
      of(detail(semver === '1.2.0' ? draft : published, semver)));
    query = new BehaviorSubject(convertToParamMap({ from: '1.1.0', to: '1.2.0' }));

    await TestBed.configureTestingModule({
      imports: [RubricCompareComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: new BehaviorSubject(convertToParamMap({ rubricId: 'piqi.core' })), queryParamMap: query }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(RubricCompareComponent);
    component = fixture.componentInstance;
  });

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('loads the two versions in the address and says what changed between them', () => {
    fixture.detectChanges();

    expect(rubricService.getVersion.calls.allArgs()).toEqual([['piqi.core', '1.1.0'], ['piqi.core', '1.2.0']]);
    expect(text()).toContain('1 setting changed');
    expect(text()).toContain('1 check added');
    expect(text()).toContain('1 removed');
    expect(text()).toContain('1 changed');
    expect(text()).toContain('PIQI Core, revised');
    expect(text()).toContain('birthdate');
    expect(text()).toContain('Checks removed (1)');
    expect(component.comparison?.changed[0]).toEqual(
      { id: 'name', changes: [{ label: 'Severity', before: 'Error', after: 'Warning' }] });
  });

  it('chooses the newest against the active version when the address does not say, and puts that in the address', () => {
    query.next(convertToParamMap({}));

    fixture.detectChanges();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining(
      { queryParams: { from: '1.1.0', to: '1.2.0' }, replaceUrl: true }));
    expect(rubricService.getVersion).not.toHaveBeenCalled();
  });

  it('keeps the side the address did give', () => {
    query.next(convertToParamMap({ to: '1.0.0' }));

    fixture.detectChanges();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { from: '1.1.0', to: '1.0.0' } }));
  });

  it('describes each version it offers by its status, and marks the active one', () => {
    fixture.detectChanges();

    expect(component.versions.map(version => component.describe(version)))
      .toEqual(['1.2.0 · Draft', '1.1.0 · Published, active', '1.0.0 · Retired']);
  });

  it('puts a new choice in the address, and swaps the sides', () => {
    fixture.detectChanges();

    component.from = '1.0.0';
    component.choose();
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { from: '1.0.0', to: '1.2.0' } }));

    component.swap();
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { from: '1.2.0', to: '1.0.0' } }));
  });

  it('reads the rubric once, and the versions again when the choice changes', () => {
    fixture.detectChanges();

    query.next(convertToParamMap({ from: '1.0.0', to: '1.2.0' }));
    fixture.detectChanges();

    expect(rubricService.getRubric).toHaveBeenCalledTimes(1);
    expect(rubricService.getVersion.calls.mostRecent().args).toEqual(['piqi.core', '1.2.0']);
    expect(rubricService.getVersion).toHaveBeenCalledTimes(4);
  });

  it('says so when the two versions are the same', () => {
    rubricService.getVersion.and.callFake((_id: string, semver: string) => of(detail({ ...published, semver }, semver)));

    fixture.detectChanges();

    expect(component.comparison?.identical).toBeTrue();
    expect(text()).toContain('These two versions have the same settings and the same checks.');
  });

  it('asks for two different versions when the same one is chosen twice', () => {
    query.next(convertToParamMap({ from: '1.1.0', to: '1.1.0' }));

    fixture.detectChanges();

    expect(rubricService.getVersion).not.toHaveBeenCalled();
    expect(text()).toContain('Choose two different versions.');
  });

  it('says a rubric with one version has nothing to compare', () => {
    rubricService.getRubric.and.returnValue(of({ ...rubric, versions: rubric.versions!.slice(0, 1) }));
    query.next(convertToParamMap({}));

    fixture.detectChanges();

    expect(text()).toContain('This rubric has only one version');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('says so when a version in the address does not exist', () => {
    rubricService.getVersion.and.returnValue(throwError(() => new RubricApiError('Rubric version not found', 404)));

    fixture.detectChanges();

    expect(text()).toContain('The rubric or one of the versions does not exist.');
  });

  it('says so when a definition could not be read', () => {
    rubricService.getVersion.and.callFake((_id: string, semver: string) =>
      of(detail(semver === '1.2.0' ? undefined : published, semver)));

    fixture.detectChanges();

    expect(text()).toContain('The definition of one of these versions could not be read.');
  });
});
