import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryPlanConfigFormComponent } from './query-plan-config.component';

describe('QueryPlanConfigFormComponent', () => {
  let component: QueryPlanConfigFormComponent;
  let fixture: ComponentFixture<QueryPlanConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueryPlanConfigFormComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryPlanConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
