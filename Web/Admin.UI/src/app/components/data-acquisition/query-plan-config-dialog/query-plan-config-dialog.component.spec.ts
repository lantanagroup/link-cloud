import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryPlanConfigDialogComponent } from './query-plan-config-dialog.component';

describe('QueryPlanConfigDialogComponent', () => {
  let component: QueryPlanConfigDialogComponent;
  let fixture: ComponentFixture<QueryPlanConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueryPlanConfigDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryPlanConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
