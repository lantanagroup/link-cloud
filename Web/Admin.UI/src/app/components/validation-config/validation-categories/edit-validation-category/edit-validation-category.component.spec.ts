import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditValidationCategoryComponent } from './edit-validation-category.component';

describe('EditValidationCategoryComponent', () => {
  let component: EditValidationCategoryComponent;
  let fixture: ComponentFixture<EditValidationCategoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditValidationCategoryComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditValidationCategoryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
