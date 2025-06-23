import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RulesetAddEditDialogComponent } from './ruleset-add-edit-dialog.component';

describe('RulesetAddEditDialogComponent', () => {
  let component: RulesetAddEditDialogComponent;
  let fixture: ComponentFixture<RulesetAddEditDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RulesetAddEditDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RulesetAddEditDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
