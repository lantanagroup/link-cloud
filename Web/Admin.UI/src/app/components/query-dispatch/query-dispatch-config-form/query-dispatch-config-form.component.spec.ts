import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryDispatchConfigFormComponent } from './query-dispatch-config-form.component';

describe('CensusConfigFormComponent', () => {
  let component: QueryDispatchConfigFormComponent;
  let fixture: ComponentFixture<QueryDispatchConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ QueryDispatchConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryDispatchConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
