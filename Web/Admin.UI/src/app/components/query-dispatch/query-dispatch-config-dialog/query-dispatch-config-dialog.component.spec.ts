import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryDispatchConfigDialogComponent } from './query-dispatch-config-dialog.component';

describe('CensusConfigDialogComponent', () => {
  let component: QueryDispatchConfigDialogComponent;
  let fixture: ComponentFixture<QueryDispatchConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ QueryDispatchConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryDispatchConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
