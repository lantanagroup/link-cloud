import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionConfigDialogComponent } from './data-acquisition-config-dialog.component';

describe('DataAcquisitionConfigDialogComponent', () => {
  let component: DataAcquisitionConfigDialogComponent;
  let fixture: ComponentFixture<DataAcquisitionConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ DataAcquisitionConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
