import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';

import { IRubricListQuery, IRubricPage, IRubricSettings, IRubricSummary } from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { RubricListComponent, SEARCH_DEBOUNCE_MS } from './rubric-list.component';

function page(content: IRubricSummary[], overrides: Partial<IRubricPage<IRubricSummary>> = {}): IRubricPage<IRubricSummary> {
  return { content, page: 0, size: 20, totalElements: content.length, totalPages: 1, ...overrides };
}

const rubric: IRubricSummary = {
  rubricId: 'piqi.core',
  title: 'PIQI Core',
  owner: 'lcg',
  versions: [
    { rubricVersionId: 3, rubricId: 'piqi.core', semver: '2.0.0', status: 'DRAFT', checksum: 'c' },
    { rubricVersionId: 2, rubricId: 'piqi.core', semver: '1.1.0', status: 'PUBLISHED', checksum: 'b' },
    { rubricVersionId: 1, rubricId: 'piqi.core', semver: '1.0.0', status: 'PUBLISHED', checksum: 'a' }
  ]
};

describe('RubricListComponent', () => {
  let fixture: ComponentFixture<RubricListComponent>;
  let component: RubricListComponent;
  let rubricService: jasmine.SpyObj<RubricService>;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['listRubrics', 'getOwners', 'getSettings']);
    rubricService.listRubrics.and.returnValue(of(page([rubric])));
    rubricService.getOwners.and.returnValue(of(['lantana', 'lcg']));
    rubricService.getSettings.and.returnValue(of(null));

    await TestBed.configureTestingModule({
      imports: [RubricListComponent],
      providers: [provideRouter([]), { provide: RubricService, useValue: rubricService }]
    }).compileComponents();

    fixture = TestBed.createComponent(RubricListComponent);
    component = fixture.componentInstance;
  });

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const text = (): string => element().textContent ?? '';
  const lastQuery = (): IRubricListQuery => rubricService.listRubrics.calls.mostRecent().args[0] ?? {};

  it('loads the first page, sorted by rubric id, with no filters and every version of each rubric', () => {
    fixture.detectChanges();

    expect(rubricService.listRubrics).toHaveBeenCalledOnceWith({
      search: undefined, status: undefined, owner: undefined, allVersions: true,
      page: 0, size: 20, sortBy: 'rubricId', sortDirection: 'asc'
    });
  });

  it('shows the newest published and the newest draft version of each rubric', () => {
    fixture.detectChanges();

    expect(component.rows[0].published?.semver).toBe('1.1.0');
    expect(component.rows[0].draft?.semver).toBe('2.0.0');
    expect(text()).toContain('PIQI Core');
    expect(text()).toContain('1.1.0');
    expect(text()).not.toContain('1.0.0');
  });

  describe('filters', () => {
    it('goes back to the first page when a filter changes', () => {
      fixture.detectChanges();
      component.pageIndex = 3;
      component.status = 'DRAFT';
      component.owner = 'lcg';

      component.applyFilters();

      expect(lastQuery()).toEqual(jasmine.objectContaining({ status: 'DRAFT', owner: 'lcg', page: 0 }));
    });

    it('searches once typing pauses, not on every key', fakeAsync(() => {
      fixture.detectChanges();
      rubricService.listRubrics.calls.reset();

      component.search = 'p';
      component.onSearchTyped();
      tick(SEARCH_DEBOUNCE_MS - 1);
      component.search = 'piqi';
      component.onSearchTyped();
      tick(SEARCH_DEBOUNCE_MS - 1);
      expect(rubricService.listRubrics).not.toHaveBeenCalled();

      tick(1);
      expect(rubricService.listRubrics).toHaveBeenCalledTimes(1);
      expect(lastQuery()).toEqual(jasmine.objectContaining({ search: 'piqi', page: 0 }));
    }));

    it('does not search again when the text settles on what was last searched for', fakeAsync(() => {
      fixture.detectChanges();
      component.search = 'piqi';
      component.onSearchTyped();
      tick(SEARCH_DEBOUNCE_MS);
      rubricService.listRubrics.calls.reset();

      component.search = 'piqi ';
      component.onSearchTyped();
      tick(SEARCH_DEBOUNCE_MS);

      expect(rubricService.listRubrics).not.toHaveBeenCalled();
    }));

    it('sends the search trimmed, and no search for only spaces', () => {
      fixture.detectChanges();

      component.search = '  core ';
      component.applyFilters();
      expect(lastQuery().search).toBe('core');

      component.search = '   ';
      component.applyFilters();
      expect(lastQuery().search).toBeUndefined();
      expect(component.filtered).toBeFalse();
    });

    it('offers the owners the service has, and no owner filter when it has none or cannot say', () => {
      fixture.detectChanges();
      expect(element().querySelectorAll('#rubricOwnerFilter option').length).toBe(3); // Any + two owners

      rubricService.getOwners.and.returnValue(throwError(() => new RubricApiError('unavailable', 500)));
      const other = TestBed.createComponent(RubricListComponent);
      other.detectChanges();

      expect((other.nativeElement as HTMLElement).querySelector('#rubricOwnerFilter')).toBeNull();
      expect(other.componentInstance.rows.length).toBe(1); // the list itself is unaffected
    });

    it('clears every filter at once', () => {
      fixture.detectChanges();
      component.search = 'core';
      component.status = 'DRAFT';
      component.owner = 'lcg';
      component.pageIndex = 2;

      component.clearFilters();

      expect(component.filtered).toBeFalse();
      expect(lastQuery()).toEqual(jasmine.objectContaining({ search: undefined, status: undefined, owner: undefined, page: 0 }));
    });

    it('says no rubric matches, and offers to clear, when the filters find nothing', () => {
      rubricService.listRubrics.and.returnValue(of(page([])));
      component.search = 'nothing';

      fixture.detectChanges();

      expect(text()).toContain('No rubric matches.');
      expect(text()).not.toContain('There are no rubrics yet.');
    });
  });

  it('asks for the page and page size the paginator gives', () => {
    fixture.detectChanges();

    component.onPage({ pageIndex: 2, pageSize: 50, length: 200 });

    expect(lastQuery()).toEqual(jasmine.objectContaining({ page: 2, size: 50 }));
  });

  it('asks for the sort the table gives, and falls back to rubric id when the sort is cleared', () => {
    fixture.detectChanges();

    component.onSort({ active: 'updatedAt', direction: 'desc' });
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sortBy: 'updatedAt', sortDirection: 'desc', page: 0 }));

    component.onSort({ active: 'updatedAt', direction: '' });
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sortBy: 'rubricId', sortDirection: 'asc' }));
  });

  describe('the rubric the pipeline uses', () => {
    const inUse = { pipelineRubricId: 'piqi.core', pipelineEnabled: true, pipelineShadowEnabled: false } as IRubricSettings;

    it('is marked In Use, and no other is', () => {
      rubricService.getSettings.and.returnValue(of(inUse));
      rubricService.listRubrics.and.returnValue(of(page([rubric, { rubricId: 'other' }])));

      fixture.detectChanges();

      expect(component.rows.map(row => row.pipelineUse)).toEqual(['decides', null]);
      expect(element().querySelectorAll('.rubric-chip--active').length).toBe(1);
      expect(element().querySelector('.rubric-chip--active')?.textContent?.trim()).toBe('In Use');
    });

    it('says in its tooltip whether the rubric decides or is only compared', () => {
      rubricService.getSettings.and.returnValue(of({ ...inUse, pipelineEnabled: false, pipelineShadowEnabled: true }));

      fixture.detectChanges();

      expect(component.rows[0].pipelineUse).toBe('compared');
      expect(element().querySelector('.rubric-chip--active')?.getAttribute('title')).toContain('does not decide');
    });

    it('is not marked when the rubric engine does not run in the pipeline at all', () => {
      rubricService.getSettings.and.returnValue(of({ ...inUse, pipelineEnabled: false, pipelineShadowEnabled: false }));

      fixture.detectChanges();

      expect(component.rows[0].pipelineUse).toBeNull();
      expect(element().querySelector('.rubric-chip--active')).toBeNull();
    });

    it('is marked when the settings arrive after the list', () => {
      const settings = new Subject<IRubricSettings | null>();
      rubricService.getSettings.and.returnValue(settings);
      fixture.detectChanges();
      expect(component.rows[0].pipelineUse).toBeNull();

      settings.next(inUse);

      expect(component.rows[0].pipelineUse).toBe('decides');
    });

    it('is not marked when the settings could not be read', () => {
      fixture.detectChanges();

      expect(component.rows[0].pipelineUse).toBeNull();
      expect(text()).not.toContain('In Use');
    });
  });

  it('says there are no rubrics when the service has none', () => {
    rubricService.listRubrics.and.returnValue(of(page([])));

    fixture.detectChanges();

    expect(component.empty).toBeTrue();
    expect(text()).toContain('There are no rubrics yet.');
    expect(element().querySelector('table')).toBeNull();
  });

  it('shows why a load failed, and loads again on retry', () => {
    rubricService.listRubrics.and.returnValue(throwError(() => new RubricApiError('Service unavailable', 503)));
    fixture.detectChanges();

    expect(component.error).toBe('Service unavailable');
    expect(component.empty).toBeFalse();
    expect(text()).toContain('The rubrics could not be loaded.');
    expect(text()).toContain('Service unavailable');

    rubricService.listRubrics.and.returnValue(of(page([rubric])));
    element().querySelector<HTMLButtonElement>('.rubric-message .rubric-button')!.click();
    fixture.detectChanges();

    expect(component.error).toBeNull();
    expect(text()).toContain('PIQI Core');
  });

  it('ignores the answer to a request that a newer one replaced', () => {
    const first = new Subject<IRubricPage<IRubricSummary>>();
    const second = new Subject<IRubricPage<IRubricSummary>>();
    rubricService.listRubrics.and.returnValues(first, second);

    fixture.detectChanges();
    component.applyFilters();
    second.next(page([{ rubricId: 'second' }]));
    first.next(page([{ rubricId: 'first' }]));

    expect(component.rows.map(row => row.rubric.rubricId)).toEqual(['second']);
  });
});
