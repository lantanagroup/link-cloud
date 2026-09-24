import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {provideNoopAnimations} from '@angular/platform-browser/animations';
import {throwError} from 'rxjs';
import {AppConfigService} from '../../../../services/app-config.service';
import {ErrorHandlingService} from '../../../../services/error-handling.service';
import {FacilityLocation} from '../../../../services/gateway/normalization/facility-locations.service';
import {buildLocationTree, HslocLocationsListComponent} from './hsloc-locations-list.component';

describe('HslocLocationsListComponent', () => {
  let fixture: ComponentFixture<HslocLocationsListComponent>;
  let http: HttpTestingController;
  const url = '/api/normalization/facility-locations/facilities/facility-1/locations';
  const location = (id: string, parent: string | null = null): FacilityLocation => ({
    id, locationId: id, partOfId: parent, locationName: id, locationAlias: null, mappings: []
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HslocLocationsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(),
        {provide: AppConfigService, useValue: {config: {baseApiUrl: '/api'}}},
        {provide: ErrorHandlingService, useValue: {handleError: (error: unknown) => throwError(() => error)}}]
    }).compileComponents();
    fixture = TestBed.createComponent(HslocLocationsListComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('facilityId', 'facility-1');
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    http.verify();
  });

  it('loads the complete tree once and opens mapping details without another request', () => {
    const child = location('Ward', 'Hospital');
    child.mappings = [
      {id: 'unmapped', localCodeSystem: 'local', localCode: 'unknown', hslocId: null, hslocCode: null},
      {id: 'mapped', localCodeSystem: 'local', localCode: 'ward', hslocId: 'hsloc-id', hslocCode: '1026'}
    ];
    const request = http.expectOne(url);
    expect(request.request.method).toBe('GET');
    request.flush({records: [child, location('Hospital')]});
    fixture.detectChanges();
    const root = fixture.componentInstance.nodes()[0];
    expect(root.mapped).toBeFalse();
    expect(root.children[0].mapped).toBeTrue();
    fixture.nativeElement.querySelector('button[aria-label="Toggle Hospital"]').click();
    fixture.detectChanges();
    const unmappedRow = fixture.nativeElement.querySelector('.location-row.unmapped');
    const mappedRow = fixture.nativeElement.querySelector('.location-row.mapped');
    expect(unmappedRow.querySelector('.mapping-status').textContent).toContain('Unmapped');
    expect(unmappedRow.querySelector('.mapping-status mat-icon').textContent).toBe('link_off');
    expect(mappedRow.querySelector('.mapping-status').textContent).toContain('Mapped');
    expect(mappedRow.querySelector('.mapping-status mat-icon').textContent).toBe('check_circle');
    expect(getComputedStyle(mappedRow).backgroundColor).not.toBe(getComputedStyle(unmappedRow).backgroundColor);
    fixture.nativeElement.querySelector('button[aria-label="View mappings for Ward"]').click();
    fixture.detectChanges();
    expect(mappedRow.classList.contains('selected')).toBeTrue();
    expect(mappedRow.classList.contains('mapped')).toBeTrue();
    const details = fixture.nativeElement.querySelector('.mapping-details');
    expect(details.textContent).toContain('1026');
    expect(details.textContent).toContain('Unmapped');
    expect(details.querySelectorAll('tbody tr').length).toBe(2);
    expect(Array.from(details.querySelectorAll('th'), (header: any) => header.textContent))
      .toEqual(['Code system', 'Local code', 'HSLOC']);
    http.expectNone(url);
  });

  it('expands and collapses all branches and reflects individual toggles', () => {
    http.expectOne(url).flush({records: [location('Hospital'), location('Ward', 'Hospital'),
      location('Room', 'Ward'), location('Other'), location('Unit', 'Other')]});
    fixture.detectChanges();
    const click = (label: string) => {
      fixture.nativeElement.querySelector(`button[aria-label="${label}"]`).click();
      fixture.detectChanges();
    };
    const expectBranches = (icon: string) => {
      for (const name of ['Hospital', 'Ward', 'Other']) {
        expect(fixture.nativeElement.querySelector(`button[aria-label="Toggle ${name}"] mat-icon`).textContent)
          .toBe(icon);
      }
    };
    click('Expand all');
    expectBranches('expand_more');
    click('Toggle Ward');
    expect(fixture.nativeElement.querySelector('button[aria-label="Expand all"]')).not.toBeNull();
    click('Expand all');
    expectBranches('expand_more');
    click('Collapse all');
    expectBranches('chevron_right');
    expect(fixture.nativeElement.querySelector('button[aria-label="Expand all"]')).not.toBeNull();
    http.expectNone(url);
  });

  it('disables the bulk toggle when there are no branches', () => {
    http.expectOne(url).flush({records: [location('Hospital')]});
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('button[aria-label="Expand all"]').disabled).toBeTrue();
  });

  it('retains orphans and breaks cycles without losing locations', () => {
    http.expectOne(url).flush({records: []});
    const roots = buildLocationTree([location('orphan', 'missing'), location('self', 'self'),
      location('first', 'second'), location('second', 'first')]);
    const pending = [...roots];
    const ids: string[] = [];
    while (pending.length && ids.length < 10) {
      const node = pending.pop()!;
      ids.push(node.id);
      pending.push(...node.children);
    }
    expect(ids.sort()).toEqual(['first', 'orphan', 'second', 'self']);
  });

  it('shows an empty state and keeps null-only mappings unmapped', () => {
    http.expectOne(url).flush({records: []});
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No locations found.');
    const row = location('ward');
    row.mappings = [{id: 'mapping', localCodeSystem: 'local', localCode: 'ward', hslocId: null,
      hslocCode: null}];
    expect(buildLocationTree([row])[0].mapped).toBeFalse();
  });

  it('cancels obsolete facility requests', () => {
    const previous = http.expectOne(url);
    fixture.componentRef.setInput('facilityId', 'facility-2');
    fixture.detectChanges();
    expect(previous.cancelled).toBeTrue();
    http.expectOne(url.replace('facility-1', 'facility-2')).flush({records: [location('new')]});
    expect(fixture.componentInstance.nodes()[0].id).toBe('new');
  });

  it('shows an error and supports retry', () => {
    http.expectOne(url).flush({}, {status: 500, statusText: 'Error'});
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    fixture.nativeElement.querySelector('button').click();
    http.expectOne(url).flush({records: [location('recovered')]});
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('recovered');
  });
});