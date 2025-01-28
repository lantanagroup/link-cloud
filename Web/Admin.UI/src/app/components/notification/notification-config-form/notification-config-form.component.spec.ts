import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NotificationConfigFormComponent } from './notification-config-form.component';

describe('NotificationConfigFormComponent', () => {
  let component: NotificationConfigFormComponent;
  let fixture: ComponentFixture<NotificationConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ NotificationConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NotificationConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
