import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RuleAddEditDialogComponent } from './validation-rule-add-edit-dialog.component';

describe('RuleAddEditDialogComponent', () => {
  let component: RuleAddEditDialogComponent;
  let fixture: ComponentFixture<RuleAddEditDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RuleAddEditDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RuleAddEditDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
