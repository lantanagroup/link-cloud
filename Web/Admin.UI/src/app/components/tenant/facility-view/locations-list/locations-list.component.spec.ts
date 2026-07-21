import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { PageEvent } from '@angular/material/paginator';

import { LocationsListComponent } from './locations-list.component';
import { DataAcquisitionService } from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import { IPagedOrganizationLocationMapping } from '../../../../interfaces/data-acquisition/organization-location-mapping-model.interface';

describe('LocationsListComponent', () => {
  let component: LocationsListComponent;
  let fixture: ComponentFixture<LocationsListComponent>;
  let serviceSpy: jasmine.SpyObj<DataAcquisitionService>;

  function pagedResult(overrides: Partial<IPagedOrganizationLocationMapping> = {}): IPagedOrganizationLocationMapping {
    return {
      records: [
        {
          locationMappingId: 1,
          facilityId: 'facility-1',
          locationId: 'Location/123',
          locationName: 'Main Campus',
          locationAlias: 'MC',
          partOfValue: 'Location/999',
          partOfId: 2,
          isOrgLocation: true,
          isActive: true
        }
      ],
      metadata: { pageSize: 10, pageNumber: 0, totalCount: 1, totalPages: 1 },
      ...overrides
    };
  }

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj<DataAcquisitionService>('DataAcquisitionService', ['getLocationMappings']);
    serviceSpy.getLocationMappings.and.returnValue(of(pagedResult()));

    await TestBed.configureTestingModule({
      imports: [LocationsListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: DataAcquisitionService, useValue: serviceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LocationsListComponent);
    component = fixture.componentInstance;
    component.facilityId = 'facility-1';
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads the facility locations on init and populates the table + pagination', () => {
    fixture.detectChanges(); // triggers ngOnInit

    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith('facility-1', 0, 10, jasmine.any(Object));
    expect(component.dataSource.data.length).toBe(1);
    expect(component.paginationMetadata.totalCount).toBe(1);
    expect(component.isLoading).toBeFalse();
    expect(component.errorMessage).toBeNull();
  });

  it('does not call the service without a facilityId', () => {
    component.facilityId = '';
    fixture.detectChanges();
    expect(serviceSpy.getLocationMappings).not.toHaveBeenCalled();
  });

  it('passes all the searchable text filters and org-location filter through to the service', () => {
    fixture.detectChanges();
    serviceSpy.getLocationMappings.calls.reset();

    component.locationIdFilter = 'Location/123';
    component.locationNameFilter = 'Main';
    component.locationAliasFilter = 'MC';
    component.partOfValueFilter = 'Location/999';
    component.isOrgLocationFilter = 'true';
    component.applyFilters();

    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10,
      {
        locationId: 'Location/123',
        locationName: 'Main',
        locationAlias: 'MC',
        partOfValue: 'Location/999',
        isOrgLocation: true,
        isActive: true
      }
    );
  });

  it('treats empty text and "Any" tri-state filters as undefined', () => {
    fixture.detectChanges();
    serviceSpy.getLocationMappings.calls.reset();

    component.isOrgLocationFilter = '';
    component.applyFilters();

    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10,
      {
        locationId: undefined,
        locationName: undefined,
        locationAlias: undefined,
        partOfValue: undefined,
        isOrgLocation: undefined,
        isActive: true
      }
    );
  });

  it('shows only active locations by default and includes inactive when the box is checked', () => {
    fixture.detectChanges();
    serviceSpy.getLocationMappings.calls.reset();

    // Default: active-only.
    component.applyFilters();
    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.objectContaining({ isActive: true })
    );

    // Checked: no active filter (all locations).
    serviceSpy.getLocationMappings.calls.reset();
    component.showInactive = true;
    component.applyFilters();
    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.objectContaining({ isActive: undefined })
    );
  });

  it('resets to the first page when filters are applied', () => {
    fixture.detectChanges();
    component.paginationMetadata.pageNumber = 3;

    component.applyFilters();

    expect(component.paginationMetadata.pageNumber).toBe(0);
  });

  it('requests the selected page on page change', () => {
    fixture.detectChanges();
    serviceSpy.getLocationMappings.calls.reset();

    component.onPageChange({ pageIndex: 2, pageSize: 20, length: 100 } as PageEvent);

    expect(component.paginationMetadata.pageNumber).toBe(2);
    expect(component.paginationMetadata.pageSize).toBe(20);
    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith('facility-1', 2, 20, jasmine.any(Object));
  });

  it('debounces free-text filter input into a single request', fakeAsync(() => {
    fixture.detectChanges();
    serviceSpy.getLocationMappings.calls.reset();

    component.locationNameFilter = 'a';
    component.onTextFilterChange();
    component.locationNameFilter = 'ab';
    component.onTextFilterChange();
    component.locationNameFilter = 'abc';
    component.onTextFilterChange();
    tick(300);

    expect(serviceSpy.getLocationMappings).toHaveBeenCalledTimes(1);
    expect(serviceSpy.getLocationMappings).toHaveBeenCalledWith(
      'facility-1', 0, 10, jasmine.objectContaining({ locationName: 'abc' })
    );
  }));

  it('surfaces backend errors on the tab and clears the data', () => {
    serviceSpy.getLocationMappings.and.returnValue(throwError(() => ({ message: 'Boom - trace-123' })));

    fixture.detectChanges();

    expect(component.errorMessage).toBe('Boom - trace-123');
    expect(component.dataSource.data.length).toBe(0);
    expect(component.isLoading).toBeFalse();
  });

  it('clearFilters resets all filters and reloads', () => {
    fixture.detectChanges();
    component.locationIdFilter = 'x';
    component.locationNameFilter = 'name';
    component.locationAliasFilter = 'alias';
    component.partOfValueFilter = 'part';
    component.isOrgLocationFilter = 'true';
    component.showInactive = true;
    expect(component.hasActiveFilters()).toBeTrue();

    component.clearFilters();

    expect(component.locationIdFilter).toBe('');
    expect(component.locationNameFilter).toBe('');
    expect(component.locationAliasFilter).toBe('');
    expect(component.partOfValueFilter).toBe('');
    expect(component.isOrgLocationFilter).toBe('');
    expect(component.showInactive).toBeFalse();
    expect(component.hasActiveFilters()).toBeFalse();
  });
});
