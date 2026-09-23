import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {provideNoopAnimations} from '@angular/platform-browser/animations';
import {MatDialog} from '@angular/material/dialog';
import {of} from 'rxjs';
import {ToastrService} from 'ngx-toastr';
import {ErrorHandlingService} from '../../services/error-handling.service';
import {HslocComponent} from './hsloc.component';
import {Hsloc} from '../../services/gateway/normalization/hsloc.service';
import {AppConfigService} from '../../services/app-config.service';

describe('HslocComponent', () => {
  let fixture: ComponentFixture<HslocComponent>;
  let component: HslocComponent;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;
  const url = '/api/normalization/HSLOC';
  const rows: Hsloc[] = [
    {id: 'active-id', hslocCode: '1026', cdcCode: 'ICU', shortDescription: 'Critical care',
      longDescription: 'Medical critical care', version: '2026', isActive: true},
    {id: 'inactive-id', hslocCode: '1000', cdcCode: 'WARD', shortDescription: 'Ward',
      longDescription: 'Retired ward', version: '2025', isActive: false}
  ];

  beforeEach(async () => {
    dialog = jasmine.createSpyObj('MatDialog', ['open']);
    await TestBed.configureTestingModule({
      imports: [HslocComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(),
        {provide: ToastrService, useValue: jasmine.createSpyObj('ToastrService', ['error'])},
        {provide: AppConfigService, useValue: {config: {baseApiUrl: '/api'}}},
        {provide: MatDialog, useValue: dialog}]
    }).overrideComponent(HslocComponent, {
      add: {providers: [{provide: MatDialog, useValue: dialog}]}
    }).compileComponents();
    fixture = TestBed.createComponent(HslocComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function loadRows(): void {
    const request = http.expectOne(`${url}?includeInactive=true`);
    expect(request.request.method).toBe('GET');
    request.flush(rows);
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
  }

  it('loads all versions but initially displays only active codes', () => {
    loadRows();
    expect(component.dataSource.filteredData).toEqual([rows[0]]);
    expect(component.versions).toEqual(['2025', '2026']);
    expect(fixture.nativeElement.textContent).toContain('Critical care');
    expect(fixture.nativeElement.textContent).not.toContain('Retired ward');
    expect(component.dataSource.paginator).toBe(component.paginator);
    expect(component.dataSource.sort).toBe(component.sort);
    expect(component.busy).toBeFalse();
  });

  it('combines case-insensitive search, version, and inactive filters', () => {
    loadRows();
    component.includeInactive = true;
    component.version = '2025';
    component.search = ' RETIRED ';
    component.applyFilter();
    expect(component.dataSource.filteredData).toEqual([rows[1]]);
    component.version = '2026';
    component.applyFilter();
    expect(component.dataSource.filteredData).toEqual([]);
  });

  it('uploads multipart CSV with trimmed versions and refreshes the catalog', () => {
    loadRows();
    component.oldVersion = ' 2025 ';
    component.newVersion = ' 2026 ';
    component.file = new File(['CDCCode,ShortDescription,HSLOCCode,LongDescription\nICU,Critical care,1026,Medical critical care'], 'hsloc.csv');
    const input = document.createElement('input');
    component.upload(input);
    const request = http.expectOne(url);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.get('OldVersion')).toBe('2025');
    expect(request.request.body.get('NewVersion')).toBe('2026');
    expect(request.request.body.get('CsvFile').name).toBe('hsloc.csv');
    expect(request.request.headers.has('Content-Type')).toBeFalse();
    request.flush(null, {status: 204, statusText: 'No Content'});
    loadRows();
    expect(component.file).toBeNull();
    expect(component.newVersion).toBe('');
    expect(component.success).toContain('updated');
  });

  it('rejects empty files and non-CSV files', () => {
    loadRows();
    for (const file of [new File([], 'empty.csv'), new File(['invalid'], 'invalid.txt')]) {
      const input = document.createElement('input');
      Object.defineProperty(input, 'files', {value: [file]});
      component.selectFile({target: input} as unknown as Event);
      expect(component.file).toBeNull();
      expect(component.fileError).toBe('Select a non-empty CSV file.');
    }
  });

  it('does not upload without required values', () => {
    loadRows();
    component.upload(document.createElement('input'));
    http.expectNone(url);
  });

  it('preserves the upload after an API error and displays Problem Details', () => {
    loadRows();
    component.oldVersion = '2025';
    component.newVersion = '2026';
    const file = new File(['invalid'], 'hsloc.csv');
    component.file = file;
    component.upload(document.createElement('input'));
    http.expectOne(url).flush({detail: 'Invalid CSV headers.'}, {status: 400, statusText: 'Bad Request'});
    expect(component.file).toBe(file);
    expect(TestBed.inject(ToastrService).error).toHaveBeenCalledOnceWith(
      'Invalid CSV headers.', 'Request failed (400)', jasmine.any(Object));
    expect(component.busy).toBeFalse();
  });

  it('does not delete when confirmation is cancelled', () => {
    loadRows();
    dialog.open.and.returnValue({afterClosed: () => of(false)} as any);
    component.deleteCode(rows[0]);
    http.expectNone(`${url}/active-id`);
  });

  it('deletes a confirmed code and reloads', () => {
    loadRows();
    dialog.open.and.returnValue({afterClosed: () => of(true)} as any);
    component.deleteCode(rows[0]);
    const request = http.expectOne(`${url}/active-id`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, {status: 204, statusText: 'No Content'});
    loadRows();
    expect(component.success).toContain('deleted');
  });

  it('explains when a code is in use without exposing database details', () => {
    loadRows();
    const handler = TestBed.inject(ErrorHandlingService);
    const handleError = spyOn(handler, 'handleError').and.callThrough();
    const formatError = spyOn(handler, 'formatError').and.callThrough();
    dialog.open.and.returnValue({afterClosed: () => of(true)} as any);
    component.deleteCode(rows[0]);
    const detail = 'The DELETE statement conflicted with the REFERENCE constraint "FK_FacilityLocationLocalCodeMapping_HSLOC". ' +
      'The conflict occurred in database "link-normalization", table "dbo.FacilityLocationLocalCodeMappings", column \'HSLOCId\'. The statement has been terminated.';
    http.expectOne(`${url}/active-id`).flush({detail, traceId: 'delete-trace'}, {status: 500, statusText: 'Server Error'});
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    const toast = TestBed.inject(ToastrService).error as jasmine.Spy;
    expect(toast).toHaveBeenCalledTimes(1);
    const alert = toast.calls.mostRecent().args[0];
    expect(alert).toContain('This HSLOC code cannot be deleted because it is used by one or more facility location mappings.');
    expect(alert).toContain('Update or remove those mappings before deleting this code.');
    expect(alert).not.toContain('FK_');
    expect(alert).not.toContain('link-normalization');
    expect(alert).toContain('Trace ID: delete-trace');
    expect(handleError).toHaveBeenCalledTimes(1);
    expect(formatError).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('This HSLOC code cannot be deleted');
    expect(component.dataSource.data).toEqual(rows);
    expect(component.success).toBe('');
    expect(component.busy).toBeFalse();
  });

  it('uses a safe fallback for other deletion failures', () => {
    loadRows();
    dialog.open.and.returnValue({afterClosed: () => of(true)} as any);
    component.deleteCode(rows[0]);
    http.expectOne(`${url}/active-id`).flush({detail: 'Internal database failure'}, {status: 500, statusText: 'Server Error'});
    expect(TestBed.inject(ToastrService).error).toHaveBeenCalledOnceWith(
      'Unable to delete the HSLOC code. Please try again. If the problem persists, contact your administrator.',
      'Unable to delete the HSLOC code (500)', jasmine.any(Object));
    expect(component.dataSource.data).toEqual(rows);
    expect(component.busy).toBeFalse();
  });

  it('shows load failures and allows a retry', () => {
    http.expectOne(`${url}?includeInactive=true`).flush({detail: 'Unable to load HSLOC codes.'}, {status: 500, statusText: 'Server Error'});
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(TestBed.inject(ToastrService).error).toHaveBeenCalledOnceWith(
      'Unable to load HSLOC codes.', 'Request failed (500)', jasmine.any(Object));
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(component.loadFailed).toBeTrue();
    expect(component.loaded).toBeFalse();
    expect(component.busy).toBeFalse();
    component.load();
    loadRows();
    expect(component.loadFailed).toBeFalse();
  });
});