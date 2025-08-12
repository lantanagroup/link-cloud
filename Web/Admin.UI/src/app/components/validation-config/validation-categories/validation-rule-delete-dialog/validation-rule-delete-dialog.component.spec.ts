import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ValidationRuleDeleteDialogComponent } from './validation-rule-delete-dialog.component';

describe('ValidationRuleDeleteDialogComponent', () => {
  let component: ValidationRuleDeleteDialogComponent;
  let fixture: ComponentFixture<ValidationRuleDeleteDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ValidationRuleDeleteDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ValidationRuleDeleteDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
