import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionAuthenticationConfigFormComponent } from './data-acquisition-authentication-config-form.component';

describe('DataAcquisitionAuthenticationConfigFormComponent', () => {
  let component: DataAcquisitionAuthenticationConfigFormComponent;
  let fixture: ComponentFixture<DataAcquisitionAuthenticationConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionAuthenticationConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionAuthenticationConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
