import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionAuthenticationConfigDialogComponent } from './data-acquisition-authentication-config-dialog.component';

describe('DataAcquisitionAuthenticationConfigDialogComponent', () => {
  let component: DataAcquisitionAuthenticationConfigDialogComponent;
  let fixture: ComponentFixture<DataAcquisitionAuthenticationConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionAuthenticationConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionAuthenticationConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
