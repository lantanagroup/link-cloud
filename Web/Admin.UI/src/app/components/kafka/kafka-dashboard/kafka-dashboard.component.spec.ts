import { ComponentFixture, TestBed } from '@angular/core/testing';

import { KafkaDashboardComponent } from './kafka-dashboard.component';

describe('KafkaDashboardComponent', () => {
  let component: KafkaDashboardComponent;
  let fixture: ComponentFixture<KafkaDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KafkaDashboardComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(KafkaDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
