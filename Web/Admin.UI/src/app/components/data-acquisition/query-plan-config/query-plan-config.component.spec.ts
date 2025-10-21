import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';

import { QueryPlanConfigFormComponent } from './query-plan-config.component';

describe('QueryPlanConfigFormComponent', () => {
  let component: QueryPlanConfigFormComponent;
  let fixture: ComponentFixture<QueryPlanConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QueryPlanConfigFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideToastr()
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(QueryPlanConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // Location resolution requirement banner is deferred until the form is touched, so it does
  // not appear pre-emptively when a plan is first loaded or selected from the dropdown.
  describe('location resolution requirement banner deferral', () => {
    // Puts the component in a state where the underlying rule is violated: location resolution
    // is active but the initial queries are missing both Encounter and Location.
    function makeViolated(): void {
      component.locationResolutionActive = true;
      component.initialQueries = [
        { resourceType: 'Patient', queryConfigType: 'Parameter' } as any
      ];
    }

    it('does not show the banner on a freshly loaded/untouched plan even when the rule is violated', () => {
      makeViolated();

      expect(component.locationResolutionViolation).toBeTrue();
      expect(component.formTouched).toBeFalse();
      expect(component.showLocationResolutionViolation).toBeFalse();
    });

    it('reveals the banner once the user edits the initial queries', () => {
      makeViolated();

      // Deleting a query is a genuine user interaction, which marks the form touched.
      component.deleteInitialQuery(0);

      expect(component.formTouched).toBeTrue();
      expect(component.showLocationResolutionViolation).toBeTrue();
    });

    it('reveals the banner after a blocked save attempt on a non-compliant plan', () => {
      makeViolated();

      component.submitConfiguration();

      expect(component.formTouched).toBeTrue();
      expect(component.showLocationResolutionViolation).toBeTrue();
    });

    it('keeps the banner hidden once Encounter and Location are present, even when touched', () => {
      component.locationResolutionActive = true;
      component.formTouched = true;
      component.initialQueries = [
        { resourceType: 'Encounter', queryConfigType: 'Parameter' } as any,
        { resourceType: 'Location', queryConfigType: 'Parameter' } as any
      ];

      expect(component.locationResolutionViolation).toBeFalse();
      expect(component.showLocationResolutionViolation).toBeFalse();
    });
  });
});
