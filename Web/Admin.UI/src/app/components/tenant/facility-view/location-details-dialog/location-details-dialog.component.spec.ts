import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';

import { LocationDetailsDialogComponent } from './location-details-dialog.component';
import { DataAcquisitionService } from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import { IOrganizationLocationMappingModel } from '../../../../interfaces/data-acquisition/organization-location-mapping-model.interface';

describe('LocationDetailsDialogComponent', () => {
  let component: LocationDetailsDialogComponent;
  let fixture: ComponentFixture<LocationDetailsDialogComponent>;
  let serviceSpy: jasmine.SpyObj<DataAcquisitionService>;

  const location: IOrganizationLocationMappingModel = {
    locationMappingId: 42,
    facilityId: 'facility-1',
    locationId: 'Loc-A',
    locationName: 'Main Campus',
    locationAlias: 'MC',
    partOfValue: 'Location/999',
    partOfId: 2,
    isOrgLocation: true,
    isActive: true
  };

  async function setup(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LocationDetailsDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: DataAcquisitionService, useValue: serviceSpy },
        { provide: MAT_DIALOG_DATA, useValue: { locationMappingId: 42 } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LocationDetailsDialogComponent);
    component = fixture.componentInstance;
  }

  beforeEach(() => {
    serviceSpy = jasmine.createSpyObj<DataAcquisitionService>('DataAcquisitionService', ['getLocationMappingById']);
  });

  it('loads the location by id on init and exposes it', async () => {
    serviceSpy.getLocationMappingById.and.returnValue(of(location));
    await setup();

    fixture.detectChanges();

    expect(serviceSpy.getLocationMappingById).toHaveBeenCalledWith(42);
    expect(component.location).toEqual(location);
    expect(component.isLoading).toBeFalse();
    expect(component.errorMessage).toBeNull();
  });

  it('surfaces an error when the fetch fails', async () => {
    serviceSpy.getLocationMappingById.and.returnValue(throwError(() => ({ message: 'Boom - trace-1' })));
    await setup();

    fixture.detectChanges();

    expect(component.errorMessage).toBe('Boom - trace-1');
    expect(component.location).toBeNull();
    expect(component.isLoading).toBeFalse();
  });
});
