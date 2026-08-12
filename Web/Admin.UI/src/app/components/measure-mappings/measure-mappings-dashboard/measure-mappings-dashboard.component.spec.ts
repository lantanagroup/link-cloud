import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of } from 'rxjs';

import { MeasureMappingsDashboardComponent } from './measure-mappings-dashboard.component';
import { MeasureMappingDialogComponent } from '../measure-mapping-dialog/measure-mapping-dialog.component';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { FormMode } from '../../../models/FormMode.enum';
import { Frequency, IMeasureMapping, IPagedMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

describe('MeasureMappingsDashboardComponent', () => {
  let component: MeasureMappingsDashboardComponent;
  let fixture: ComponentFixture<MeasureMappingsDashboardComponent>;
  let measureMappingService: jasmine.SpyObj<MeasureMappingService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  const mappings: IMeasureMapping[] = [
    { id: 'mm-1', measure: 'ACH', dqm: 'NHSNAcuteCareHospitalDailyInitialPopulation', frequency: Frequency.Monthly },
    { id: 'mm-2', measure: 'TRIM', dqm: 'AchMonthly', frequency: Frequency.Weekly }
  ];

  const pagedResponse: IPagedMeasureMapping = {
    records: mappings,
    metadata: { pageSize: 10, pageNumber: 0, totalCount: 2, totalPages: 1 }
  };

  beforeEach(async () => {
    measureMappingService = jasmine.createSpyObj<MeasureMappingService>(
      'MeasureMappingService', ['searchMeasureMappings', 'deleteMeasureMapping']);
    measureMappingService.searchMeasureMappings.and.returnValue(of(pagedResponse));

    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [MeasureMappingsDashboardComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [
        { provide: MeasureMappingService, useValue: measureMappingService },
        { provide: MatDialog, useValue: dialog }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MeasureMappingsDashboardComponent);
    component = fixture.componentInstance;
  });

  function dialogClosingWith(result: unknown): void {
    dialog.open.and.returnValue({ afterClosed: () => of(result) } as any);
  }

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads measure mappings and clears loading once they arrive', () => {
    fixture.detectChanges();

    expect(component.measureMappings.length).toBe(2);
    expect(component.loading).toBeFalse();
    expect(component.paginationMetadata.totalCount).toBe(2);
  });

  it('treats a null (204) response as an empty page', () => {
    measureMappingService.searchMeasureMappings.and.returnValue(of(null));

    fixture.detectChanges();

    expect(component.measureMappings).toEqual([]);
    expect(component.paginationMetadata.totalCount).toBe(0);
  });

  it('opens the dialog in Create mode', () => {
    dialogClosingWith(null);
    fixture.detectChanges();

    component.onAdd();

    const config = dialog.open.calls.mostRecent().args[1] as any;
    expect(dialog.open.calls.mostRecent().args[0]).toBe(MeasureMappingDialogComponent);
    expect(config.data.formMode).toBe(FormMode.Create);
  });

  it('opens the dialog in Edit mode with the row', () => {
    dialogClosingWith(null);
    fixture.detectChanges();

    component.onEdit(mappings[0]);

    const config = dialog.open.calls.mostRecent().args[1] as any;
    expect(config.data.formMode).toBe(FormMode.Edit);
    expect(config.data.measureMapping).toBe(mappings[0]);
  });

  it('refreshes the list after a saved edit', () => {
    dialogClosingWith({ success: true, message: '' });
    fixture.detectChanges();
    const before = measureMappingService.searchMeasureMappings.calls.count();

    component.onEdit(mappings[1]);

    expect(measureMappingService.searchMeasureMappings.calls.count()).toBe(before + 1);
  });

  it('does not refresh when the dialog is dismissed', () => {
    dialogClosingWith(null);
    fixture.detectChanges();
    const before = measureMappingService.searchMeasureMappings.calls.count();

    component.onEdit(mappings[1]);

    expect(measureMappingService.searchMeasureMappings.calls.count()).toBe(before);
  });

  it('resets to the first page when a filter changes', () => {
    fixture.detectChanges();
    component.paginationMetadata.pageNumber = 3;

    component.filterMeasure = 'ACH';
    component.onSearchChange();

    expect(component.paginationMetadata.pageNumber).toBe(0);
  });

  it('deletes after confirmation and refreshes the list', () => {
    dialog.open.and.returnValue({ afterClosed: () => of(true) } as any);
    measureMappingService.deleteMeasureMapping.and.returnValue(of({}));
    fixture.detectChanges();
    const before = measureMappingService.searchMeasureMappings.calls.count();

    component.onDelete(mappings[0]);

    expect(measureMappingService.deleteMeasureMapping).toHaveBeenCalledWith('mm-1');
    expect(measureMappingService.searchMeasureMappings.calls.count()).toBe(before + 1);
  });

  it('does not delete when the confirmation is dismissed', () => {
    dialog.open.and.returnValue({ afterClosed: () => of(false) } as any);
    fixture.detectChanges();

    component.onDelete(mappings[0]);

    expect(measureMappingService.deleteMeasureMapping).not.toHaveBeenCalled();
  });

  it('offers add, edit and delete controls', () => {
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[aria-label="Add measure mapping"]')).not.toBeNull();
    expect(root.querySelector('[aria-label="Edit Measure Mapping"]')).not.toBeNull();
    expect(root.querySelector('[aria-label="Delete Measure Mapping"]')).not.toBeNull();
  });
});
