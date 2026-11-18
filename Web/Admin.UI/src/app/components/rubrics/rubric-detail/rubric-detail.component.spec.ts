import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { IRubricDetail, IRubricLifecycleEvent, IRubricSettings } from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { RubricDetailComponent } from './rubric-detail.component';

const rubric: IRubricDetail = {
  rubricId: 'piqi.core',
  title: 'PIQI Core',
  owner: 'lcg',
  latestPublishedSemver: '1.1.0',
  versions: [
    { rubricVersionId: 3, rubricId: 'piqi.core', semver: '2.0.0', status: 'DRAFT', checksum: 'c' },
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'b', checkCount: 97 },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'a' }
  ]
};

const events: IRubricLifecycleEvent[] = [
  { eventId: 'e-1', semver: '1.1.0', action: 'PUBLISHED', actor: 'reviewer', checksum: 'b', occurredAt: '2026-09-03T16:30:00Z' }
];

describe('RubricDetailComponent', () => {
  let fixture: ComponentFixture<RubricDetailComponent>;
  let component: RubricDetailComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let params: BehaviorSubject<ParamMap>;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['getRubric', 'getEvents', 'getSettings']);
    rubricService.getSettings.and.returnValue(of(null));
    rubricService.getRubric.and.returnValue(of(rubric));
    rubricService.getEvents.and.returnValue(of(events));
    params = new BehaviorSubject(convertToParamMap({ rubricId: 'piqi.core' }));

    await TestBed.configureTestingModule({
      imports: [RubricDetailComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        { provide: ActivatedRoute, useValue: { paramMap: params } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RubricDetailComponent);
    component = fixture.componentInstance;
  });

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('loads the rubric and its history for the id in the route', () => {
    fixture.detectChanges();

    expect(rubricService.getRubric).toHaveBeenCalledOnceWith('piqi.core');
    expect(rubricService.getEvents).toHaveBeenCalledOnceWith('piqi.core');
    expect(component.versions.length).toBe(3);
    expect(component.events).toEqual(events);
    expect(text()).toContain('PIQI Core');
    expect(text()).toContain('Active version 1.1.0');
  });

  it('shows how many checks each version has, and a dash where the service did not say', () => {
    fixture.detectChanges();

    const cells = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('td.mat-column-checks'))
      .map(cell => cell.textContent?.trim());
    expect(cells).toEqual(['—', '97', '—']);
  });

  it('marks the rubric the pipeline uses as In Use, and follows the route to one it does not', () => {
    rubricService.getSettings.and.returnValue(of(
      { pipelineRubricId: 'piqi.core', pipelineEnabled: false, pipelineShadowEnabled: true } as IRubricSettings));
    fixture.detectChanges();
    expect(component.pipelineUse).toBe('compared');
    const chip = (fixture.nativeElement as HTMLElement).querySelector('.rubric-chip--on-banner');
    expect(chip?.textContent?.trim()).toBe('In Use');
    expect(chip?.getAttribute('title')).toContain('does not decide');

    params.next(convertToParamMap({ rubricId: 'other' }));
    fixture.detectChanges();
    expect(component.pipelineUse).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.rubric-chip--on-banner')).toBeNull();
  });

  it('does not mark the rubric when the rubric engine does not run in the pipeline, or the settings could not be read', () => {
    fixture.detectChanges();
    expect(component.pipelineUse).toBeNull();

    rubricService.getSettings.and.returnValue(of(
      { pipelineRubricId: 'piqi.core', pipelineEnabled: false, pipelineShadowEnabled: false } as IRubricSettings));
    const other = TestBed.createComponent(RubricDetailComponent);
    other.detectChanges();
    expect(other.componentInstance.pipelineUse).toBeNull();
  });

  it('marks only the newest published version as the active one', () => {
    fixture.detectChanges();

    expect(component.versions.map(version => component.isActive(version))).toEqual([false, true, false]);
  });

  it('says so when no version is published', () => {
    rubricService.getRubric.and.returnValue(of({ ...rubric, latestPublishedSemver: undefined }));

    fixture.detectChanges();

    expect(text()).toContain('No published version');
    expect(component.versions.some(version => component.isActive(version))).toBeFalse();
  });

  it('still shows the versions when the history cannot be loaded', () => {
    rubricService.getEvents.and.returnValue(throwError(() => new RubricApiError('History unavailable', 500)));

    fixture.detectChanges();

    expect(component.rubric).toEqual(rubric);
    expect(component.error).toBeNull();
    expect(component.eventsError).toBe('History unavailable');
    expect(component.events).toEqual([]);
  });

  it('says the rubric does not exist on a 404', () => {
    rubricService.getRubric.and.returnValue(throwError(() => new RubricApiError('Rubric not found: piqi.core', 404)));

    fixture.detectChanges();

    expect(component.notFound).toBeTrue();
    expect(component.rubric).toBeNull();
    expect(text()).toContain('There is no rubric with the id "piqi.core".');
  });

  it('shows why the rubric could not be loaded on any other failure', () => {
    rubricService.getRubric.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));

    fixture.detectChanges();

    expect(component.notFound).toBeFalse();
    expect(text()).toContain('The rubric could not be loaded.');
    expect(text()).toContain('Service unavailable');
  });

  it('loads again when the route changes to another rubric, even after a failed load', () => {
    rubricService.getRubric.and.returnValue(throwError(() => new RubricApiError('Rubric not found', 404)));
    fixture.detectChanges();
    expect(component.notFound).toBeTrue();

    rubricService.getRubric.and.returnValue(of({ ...rubric, rubricId: 'other', title: 'Other' }));
    params.next(convertToParamMap({ rubricId: 'other' }));
    fixture.detectChanges();

    expect(rubricService.getRubric.calls.mostRecent().args).toEqual(['other']);
    expect(component.notFound).toBeFalse();
    expect(component.rubric?.title).toBe('Other');
  });

  describe('what it offers for each version', () => {
    const actions = (): string[][] =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('td.mat-column-actions'))
        .map(cell => Array.from(cell.querySelectorAll('a')).map(link => link.textContent?.trim() ?? ''));

    it('edit, dry run, publish and delete for a draft; dry run and retire for a published version', () => {
      fixture.detectChanges();

      expect(actions()).toEqual([
        ['Edit', 'Dry run', 'Publish', 'Delete'],
        ['Dry run', 'Retire'],
        ['Dry run', 'Retire']
      ]);
    });

    it('nothing for a retired version', () => {
      rubricService.getRubric.and.returnValue(of({
        ...rubric,
        versions: [{ rubricVersionId: 1, rubricId: 'piqi.core', semver: '0.9.0', status: 'RETIRED', checksum: 'z' }]
      }));

      fixture.detectChanges();

      expect(actions()).toEqual([[]]);
    });

    it('sends publish, retire and delete to the version\'s page to be confirmed there', () => {
      fixture.detectChanges();

      const publish = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('td.mat-column-actions a'))
        .find(link => link.textContent?.trim() === 'Publish') as HTMLAnchorElement;

      expect(publish.getAttribute('href')).toBe('/rubrics/piqi.core/versions/2.0.0?do=publish');
      expect(publish.getAttribute('aria-label')).toBe('Publish version 2.0.0');
    });

    it('offers to compare versions only when there is more than one', () => {
      fixture.detectChanges();
      expect(text()).toContain('Compare versions');

      rubricService.getRubric.and.returnValue(of({ ...rubric, versions: rubric.versions!.slice(0, 1) }));
      params.next(convertToParamMap({ rubricId: 'other' }));
      fixture.detectChanges();
      expect(text()).not.toContain('Compare versions');
    });
  });
});
