import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ValidationCategoriesComponent } from './validation-categories-list.component';

describe('ValidationCategoriesComponent', () => {
  let component: ValidationCategoriesComponent;
  let fixture: ComponentFixture<ValidationCategoriesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ValidationCategoriesComponent]
    })
      .compileComponents();

    fixture = TestBed.createComponent(ValidationCategoriesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
