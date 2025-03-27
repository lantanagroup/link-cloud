import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryPlanConfigComponent } from './query-plan-config.component';

describe('QueryPlanConfigComponent', () => {
  let component: QueryPlanConfigComponent;
  let fixture: ComponentFixture<QueryPlanConfigComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueryPlanConfigComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryPlanConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
