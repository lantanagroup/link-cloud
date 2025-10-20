import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryConfigDialogComponent } from './query-dispatch-config-dialog.component';

describe('CensusConfigDialogComponent', () => {
  let component: QueryConfigDialogComponent;
  let fixture: ComponentFixture<QueryConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ QueryConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
