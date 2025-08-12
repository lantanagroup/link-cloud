import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OperationsListComponent } from './operations-list.component';

describe('OperationsListComponent', () => {
  let component: OperationsListComponent;
  let fixture: ComponentFixture<OperationsListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OperationsListComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OperationsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
