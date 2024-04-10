import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MeasureDefinitionDashboardComponent } from './measure-def-dashboard.component';



describe('MeasureDefinitionDashboardComponent', () => {
  let component: MeasureDefinitionDashboardComponent;
  let fixture: ComponentFixture<MeasureDefinitionDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MeasureDefinitionDashboardComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MeasureDefinitionDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
