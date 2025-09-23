import { ComponentFixture, TestBed } from '@angular/core/testing';

import { QueryConfigFormComponent } from './query-dispatch-config-form.component';

describe('CensusConfigFormComponent', () => {
  let component: QueryConfigFormComponent;
  let fixture: ComponentFixture<QueryConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ QueryConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
