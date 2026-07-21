import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';

import { EncountersListComponent } from './encounters-list.component';
import { LocationDetailsDialogComponent } from '../location-details-dialog/location-details-dialog.component';
import { DataAcquisitionService } from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import { IPagedEncounterMapping } from '../../../../interfaces/data-acquisition/encounter-mapping-model.interface';

describe('EncountersListComponent', () => {
  let component: EncountersListComponent;
  let fixture: ComponentFixture<EncountersListComponent>;
  let serviceSpy: jasmine.SpyObj<DataAcquisitionService>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;

  function pagedResult(overrides: Partial<IPagedEncounterMapping> = {}): IPagedEncounterMapping {
    return {
      records: [
        {
          encounterMappingId: 1,
          facilityId: 'facility-1',
          patientId: 'Patient/123',
          encounterId: 'Encounter/456',
          mappedToOrg: true,
          encounterLocations: [
            { encounterLocationId: 1, encounterMappingId: 1, organizationLocationMappingId: 10, locationId: 'Loc-A' },
            { encounterLocationId: 2, encounterMappingId: 1, organizationLocationMappingId: 11, locationId: 'Loc-B' }
          ]
        }
      ],
      metadata: { pageSize: 10, pageNumber: 0, totalCount: 1, totalPages: 1 },
      ...overrides
    };
  }

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj<DataAcquisitionService>('DataAcquisitionService', ['getEncounterMappings']);
    serviceSpy.getEncounterMappings.and.returnValue(of(pagedResult()));
    dialogSpy = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [EncountersListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: DataAcquisitionService, useValue: serviceSpy },
        { provide: MatDialog, useValue: dialogSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EncountersListComponent);
    component = fixture.componentInstance;
    component.facilityId = 'facility-1';
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads the facility encounters on init and populates the table + pagination', () => {
    fixture.detectChanges(); // triggers ngOnInit

    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.any(Object), undefined, undefined);
    expect(component.dataSource.data.length).toBe(1);
    expect(component.paginationMetadata.totalCount).toBe(1);
    expect(component.isLoading).toBeFalse();
    expect(component.errorMessage).toBeNull();
  });

  it('does not call the service without a facilityId', () => {
    component.facilityId = '';
    fixture.detectChanges();
    expect(serviceSpy.getEncounterMappings).not.toHaveBeenCalled();
  });

  it('passes the encounter and patient filters through to the service', () => {
    fixture.detectChanges();
    serviceSpy.getEncounterMappings.calls.reset();

    component.encounterIdFilter = 'Encounter/456';
    component.patientIdFilter = 'Patient/123';
    component.applyFilters();

    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10,
      { encounterId: 'Encounter/456', patientId: 'Patient/123' },
      undefined, undefined
    );
  });

  it('treats empty text filters as undefined', () => {
    fixture.detectChanges();
    serviceSpy.getEncounterMappings.calls.reset();

    component.encounterIdFilter = '   ';
    component.patientIdFilter = '';
    component.applyFilters();

    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10,
      { encounterId: undefined, patientId: undefined },
      undefined, undefined
    );
  });

  it('maps a sort change to the API field name and order, and resets to the first page', () => {
    fixture.detectChanges();
    component.paginationMetadata.pageNumber = 3;
    serviceSpy.getEncounterMappings.calls.reset();

    component.onSortChange({ active: 'patientId', direction: 'desc' } as Sort);

    expect(component.currentSortBy).toBe('PatientId');
    expect(component.currentSortOrder).toBe(1); // 1 = Descending
    expect(component.paginationMetadata.pageNumber).toBe(0);
    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.any(Object), 'PatientId', 1);
  });

  it('clears sort back to the backend default when direction is removed', () => {
    fixture.detectChanges();
    component.currentSortBy = 'EncounterId';
    component.currentSortOrder = 0;
    serviceSpy.getEncounterMappings.calls.reset();

    component.onSortChange({ active: 'encounterId', direction: '' } as Sort);

    expect(component.currentSortBy).toBeUndefined();
    expect(component.currentSortOrder).toBeUndefined();
  });

  it('comma-joins location ids across multiple locations', () => {
    expect(component.getLocationIds(pagedResult().records[0])).toBe('Loc-A, Loc-B');
  });

  it('getLocations returns only locations with a resolvable locationId', () => {
    const record = {
      encounterMappingId: 3, facilityId: 'f', patientId: 'p', encounterId: 'e', mappedToOrg: false,
      encounterLocations: [
        { encounterLocationId: 1, encounterMappingId: 3, organizationLocationMappingId: 10, locationId: 'Loc-A' },
        { encounterLocationId: 2, encounterMappingId: 3, organizationLocationMappingId: 11, locationId: null }
      ]
    };
    const locations = component.getLocations(record);
    expect(locations.length).toBe(1);
    expect(locations[0].locationId).toBe('Loc-A');
  });

  it('opens the location-details dialog for the clicked location', () => {
    const location = { encounterLocationId: 1, encounterMappingId: 1, organizationLocationMappingId: 42, locationId: 'Loc-A' };

    component.openLocationDetails(location);

    expect(dialogSpy.open).toHaveBeenCalledWith(
      LocationDetailsDialogComponent,
      jasmine.objectContaining({ data: { locationMappingId: 42 } })
    );
  });

  it('ignores null/empty location ids when joining', () => {
    const record = {
      encounterMappingId: 2, facilityId: 'f', patientId: 'p', encounterId: 'e', mappedToOrg: false,
      encounterLocations: [
        { encounterLocationId: 1, encounterMappingId: 2, organizationLocationMappingId: 1, locationId: 'Only' },
        { encounterLocationId: 2, encounterMappingId: 2, organizationLocationMappingId: 2, locationId: null }
      ]
    };
    expect(component.getLocationIds(record)).toBe('Only');
  });

  it('requests the selected page on page change', () => {
    fixture.detectChanges();
    serviceSpy.getEncounterMappings.calls.reset();

    component.onPageChange({ pageIndex: 2, pageSize: 20, length: 100 } as PageEvent);

    expect(component.paginationMetadata.pageNumber).toBe(2);
    expect(component.paginationMetadata.pageSize).toBe(20);
    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 2, 20, jasmine.any(Object), undefined, undefined);
  });

  it('debounces free-text filter input into a single request', fakeAsync(() => {
    fixture.detectChanges();
    serviceSpy.getEncounterMappings.calls.reset();

    component.encounterIdFilter = 'a';
    component.onTextFilterChange();
    component.encounterIdFilter = 'ab';
    component.onTextFilterChange();
    component.encounterIdFilter = 'abc';
    component.onTextFilterChange();
    tick(300);

    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledTimes(1);
    expect(serviceSpy.getEncounterMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.objectContaining({ encounterId: 'abc' }), undefined, undefined
    );
  }));

  it('surfaces backend errors on the tab and clears the data', () => {
    serviceSpy.getEncounterMappings.and.returnValue(throwError(() => ({ message: 'Boom - trace-123' })));

    fixture.detectChanges();

    expect(component.errorMessage).toBe('Boom - trace-123');
    expect(component.dataSource.data.length).toBe(0);
    expect(component.isLoading).toBeFalse();
  });

  it('clearFilters resets all filters and reloads', () => {
    fixture.detectChanges();
    component.encounterIdFilter = 'x';
    component.patientIdFilter = 'y';
    expect(component.hasActiveFilters()).toBeTrue();

    component.clearFilters();

    expect(component.encounterIdFilter).toBe('');
    expect(component.patientIdFilter).toBe('');
    expect(component.hasActiveFilters()).toBeFalse();
  });
});
