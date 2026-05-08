import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CensusConfigFormComponent } from './census-config-form.component';

describe('CensusConfigFormComponent', () => {
  let component: CensusConfigFormComponent;
  let fixture: ComponentFixture<CensusConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ CensusConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CensusConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
