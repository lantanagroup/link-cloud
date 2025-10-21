import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppConfigurationDashboardComponent } from './app-configuration-dashboard.component';

describe('AppConfigurationDashboardComponent', () => {
  let component: AppConfigurationDashboardComponent;
  let fixture: ComponentFixture<AppConfigurationDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppConfigurationDashboardComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AppConfigurationDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
