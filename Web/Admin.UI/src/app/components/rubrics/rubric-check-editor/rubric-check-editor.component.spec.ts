import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IRubricCheck, IRubricDefinition } from '../../../interfaces/validation/rubric.interface';
import { RubricCheckEditorComponent } from './rubric-check-editor.component';

function check(overrides: Partial<IRubricCheck> = {}): IRubricCheck {
  return {
    id: 'c1', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: true,
    parameters: { expression: 'Patient.name.exists()' }, ...overrides
  };
}

function definition(checks: IRubricCheck[]): IRubricDefinition {
  return {
    id: 'piqi.core', semver: '1.0.0', dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-pass-fail' }, checks
  };
}

describe('RubricCheckEditorComponent', () => {
  let fixture: ComponentFixture<RubricCheckEditorComponent>;
  let component: RubricCheckEditorComponent;
  let applied: IRubricCheck[];
  let cancelled: number;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RubricCheckEditorComponent] }).compileComponents();
  });

  /** Opens the editor on a check of a draft. `index` past the end is a check being added. */
  function open(draft: IRubricDefinition, index: number, editing: IRubricCheck, adding = false): void {
    fixture = TestBed.createComponent(RubricCheckEditorComponent);
    component = fixture.componentInstance;
    applied = [];
    cancelled = 0;
    component.applied.subscribe(value => applied.push(value));
    component.cancelled.subscribe(() => cancelled++);
    fixture.componentRef.setInput('definition', draft);
    fixture.componentRef.setInput('index', index);
    fixture.componentRef.setInput('check', editing);
    fixture.componentRef.setInput('adding', adding);
    fixture.detectChanges();
  }

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const text = (): string => element().textContent ?? '';

  it('edits a copy: the check it was given is untouched until Apply', () => {
    const original = check();
    open(definition([original]), 0, original);

    component.draft.id = 'renamed';
    component.draft.parameters!.expression = 'changed';
    component.onChanged();

    expect(original.id).toBe('c1');
    expect(original.parameters?.expression).toBe('Patient.name.exists()');

    component.apply();
    expect(applied).toEqual([jasmine.objectContaining({ id: 'renamed', parameters: { expression: 'changed' } })]);
  });

  it('Cancel applies nothing', () => {
    open(definition([check()]), 0, check());
    component.draft.id = 'renamed';

    component.cancel();

    expect(cancelled).toBe(1);
    expect(applied).toEqual([]);
  });

  describe('problems', () => {
    it('are not shown on a new check before Apply is tried', () => {
      const draft = definition([]);
      open(draft, 0, { id: '', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: true, parameters: {} }, true);

      expect(component.errors('id')).toEqual([]);
      expect(text()).not.toContain('Enter an id for the check.');
    });

    it('stop the first Apply and are shown; Apply anyway keeps the check as it is', () => {
      open(definition([]), 0, { id: '', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: true, parameters: {} }, true);

      component.apply();
      fixture.detectChanges();

      expect(applied).toEqual([]);
      expect(text()).toContain('Enter an id for the check.');
      expect(text()).toContain('Enter a FHIRPath expression.');
      expect(text()).toContain('This check has 2 problems.');

      component.apply(true);
      expect(applied.length).toBe(1);
    });

    it('are shown straight away on an existing check that has them', () => {
      const broken = check({ parameters: {} });
      open(definition([broken]), 0, broken);

      expect(text()).toContain('Enter a FHIRPath expression.');
    });

    it('include an id that another check of the draft already has', () => {
      const draft = definition([check({ id: 'taken' }), check({ id: 'mine', parameters: { expression: 'other' } })]);
      open(draft, 1, draft.checks[1]);

      component.draft.id = 'taken';
      component.onChanged();
      component.apply();

      expect(component.errors('id').map(issue => issue.message)).toEqual(['Another check already has the id "taken".']);
      expect(applied).toEqual([]);
    });

    it('show the service\'s problems until the check is changed', () => {
      const existing = check();
      open(definition([existing]), 0, existing);
      fixture.componentRef.setInput('serverIssues', [
        { path: 'checks[c1]', message: 'invalid FHIRPath expression: unexpected token', field: 'parameters.expression', checkIndex: 0 }]);
      fixture.componentRef.setInput('check', { ...existing });
      fixture.detectChanges();

      expect(text()).toContain('invalid FHIRPath expression: unexpected token');

      component.draft.parameters!.expression = 'Patient.id.exists()';
      component.onChanged();
      fixture.detectChanges();

      expect(text()).not.toContain('invalid FHIRPath expression');
    });

    it('list at the top a problem that has no field of its own', () => {
      const existing = check();
      open(definition([existing]), 0, existing);
      fixture.componentRef.setInput('serverIssues', [
        { path: 'checks[c1]', message: 'duplicate of check \'other\' (same type, dimension, and parameters)', field: '', checkIndex: 0 }]);
      fixture.componentRef.setInput('check', { ...existing });
      fixture.detectChanges();

      expect(component.otherErrors.length).toBe(1);
      expect(element().querySelector('.check-editor-errors')?.textContent).toContain('duplicate of check');
    });
  });

  describe('type and dimension', () => {
    it('offers the types the form can create, and never Custom', () => {
      open(definition([check()]), 0, check());

      expect(component.types).toContain('VALUESET');
      expect(component.types).not.toContain('CUSTOM');
    });

    it('fixes the dimension for a type that needs one, and clears the old type\'s settings', () => {
      open(definition([check()]), 0, check());

      component.draft.type = 'VALUESET';
      component.onTypeChanged();

      expect(component.fixedDimension).toBe('TERMINOLOGY');
      expect(component.draft.dimension).toBe('TERMINOLOGY');
      expect(component.draft.parameters).toEqual({});
    });

    it('leaves the dimension a choice among the rubric\'s for FHIRPath', () => {
      open(definition([check()]), 0, check());

      expect(component.fixedDimension).toBeNull();
      expect(component.dimensions).toEqual(['CONFORMANCE', 'TERMINOLOGY']);
    });

    it('keeps a dimension the rubric has since dropped in the list, so the check still shows what it has', () => {
      const orphan = check({ dimension: 'CURRENCY' });
      open(definition([orphan]), 0, orphan);

      expect(component.dimensions).toEqual(['CURRENCY', 'CONFORMANCE', 'TERMINOLOGY']);
      expect(component.errors('dimension').length).toBe(1);
    });
  });

  describe('value set checks', () => {
    const valueSet = check({ id: 'vs', type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { path: 'Patient.gender', valueSet: 'http://vs' } });

    it('switch to several checks combined, starting with one to fill in', () => {
      open(definition([valueSet]), 0, valueSet);

      component.setCombined(true);

      expect(component.combined).toBeTrue();
      expect(component.draft.parameters).toEqual({ matchMode: 'ANY', checks: [{ id: '', type: 'FHIRPATH', parameters: {} }] });
    });

    it('add and remove the checks they combine', () => {
      open(definition([valueSet]), 0, valueSet);
      component.setCombined(true);

      component.addChild();
      component.draft.parameters!.checks![1].id = 'second';
      component.removeChild(0);

      expect(component.draft.parameters!.checks!.map(child => child.id)).toEqual(['second']);
    });

    it('place a problem with a combined check on that check', () => {
      open(definition([valueSet]), 0, valueSet);
      component.setCombined(true);

      component.apply();

      expect(component.childErrors(0, 'id').length).toBe(1);
      expect(component.childErrors(0, 'parameters.expression').length).toBe(1);
      // those are shown on the child, not repeated in the list at the top
      expect(component.otherErrors).toEqual([]);
    });

    it('switch back to a single element, dropping the combined checks', () => {
      const combined = check({ id: 'combo', type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { matchMode: 'ALL', checks: [{ id: 'a', type: 'FHIRPATH', parameters: { expression: 'true' } }] } });
      open(definition([combined]), 0, combined);

      component.setCombined(false);

      expect(component.draft.parameters).toEqual({});
    });
  });

  it('reads profiles one to a line, ignoring blank lines', () => {
    const conformance = check({ type: 'FHIR_CONFORMANCE', parameters: { profiles: ['http://a'] } });
    open(definition([conformance]), 0, conformance);
    expect(component.profilesText).toBe('http://a');

    component.profilesText = 'http://a\n\n  http://b  \n';
    component.onProfilesChanged();

    expect(component.draft.parameters!.profiles).toEqual(['http://a', 'http://b']);
  });

  it('keeps "validate codings" unset, yes or no', () => {
    const terminology = check({ type: 'TERMINOLOGY', dimension: 'TERMINOLOGY', parameters: {} });
    open(definition([terminology]), 0, terminology);
    expect(component.validateCodings).toBe('');

    component.onValidateCodingsChanged('false');
    expect(component.draft.parameters!.validateCodings).toBeFalse();

    component.onValidateCodingsChanged('');
    expect('validateCodings' in component.draft.parameters!).toBeFalse();
  });

  describe('a check the form cannot edit', () => {
    const custom = check({ id: 'plug', type: 'CUSTOM', parameters: { customCheckId: 'my-plugin' } });

    it('is shown as it is, with why, and no fields', () => {
      open(definition([custom]), 0, custom);

      expect(component.readOnlyReason).toContain('Custom checks');
      expect(text()).toContain('kept exactly as it is');
      expect(text()).toContain('my-plugin');
      expect(element().querySelector('#checkId')).toBeNull();
    });

    it('can only be closed', () => {
      open(definition([custom]), 0, custom);

      element().querySelector<HTMLButtonElement>('.check-editor-actions button')!.click();

      expect(cancelled).toBe(1);
      expect(applied).toEqual([]);
    });
  });
});
