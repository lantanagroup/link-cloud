import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CensusConfigDialogComponent } from './census-config-dialog.component';

describe('CensusConfigDialogComponent', () => {
  let component: CensusConfigDialogComponent;
  let fixture: ComponentFixture<CensusConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ CensusConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CensusConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
