import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OperationDialogComponent } from './operation-dialog.component';

describe('OperationDialogComponent', () => {
  let component: OperationDialogComponent;
  let fixture: ComponentFixture<OperationDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OperationDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OperationDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
