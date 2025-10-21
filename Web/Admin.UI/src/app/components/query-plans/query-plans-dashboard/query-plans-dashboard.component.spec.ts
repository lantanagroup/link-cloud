import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryPlansDashboardComponent } from './query-plans-dashboard.component';

describe('QueryPlansDashboardComponent', () => {
  let component: QueryPlansDashboardComponent;
  let fixture: ComponentFixture<QueryPlansDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueryPlansDashboardComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryPlansDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
