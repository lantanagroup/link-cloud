import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LinkAlertComponent } from './link-alert.component';

describe('LinkAlertComponent', () => {
  let component: LinkAlertComponent;
  let fixture: ComponentFixture<LinkAlertComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ LinkAlertComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LinkAlertComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
