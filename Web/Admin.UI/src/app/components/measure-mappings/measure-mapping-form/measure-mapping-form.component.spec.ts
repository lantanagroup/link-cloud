import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { MeasureMappingFormComponent } from './measure-mapping-form.component';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { MeasureDefinitionService } from '../../../services/gateway/measure-definition/measure.service';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { FormMode } from '../../../models/FormMode.enum';
import { Frequency, IMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

describe('MeasureMappingFormComponent', () => {
  let component: MeasureMappingFormComponent;
  let fixture: ComponentFixture<MeasureMappingFormComponent>;
  let measureMappingService: jasmine.SpyObj<MeasureMappingService>;
  let measureDefinitionService: jasmine.SpyObj<MeasureDefinitionService>;

  const ach: IMeasureMapping = { id: 'mm-1', measure: 'ACH', dqm: 'NHSNAcuteCareHospitalDailyInitialPopulation', frequency: Frequency.Monthly };

  beforeEach(async () => {
    measureMappingService = jasmine.createSpyObj<MeasureMappingService>('MeasureMappingService', ['createMeasureMapping', 'updateMeasureMapping']);
    measureDefinitionService = jasmine.createSpyObj<MeasureDefinitionService>('MeasureDefinitionService', ['getMeasureDefinitionConfigurations']);
    measureDefinitionService.getMeasureDefinitionConfigurations.and.returnValue(of([
      { id: 'NHSNAcuteCareHospitalDailyInitialPopulation' },
      { id: 'NHSNGlycemicControlHypoglycemicInitialPopulation' }
    ] as IMeasureDefinitionConfigModel[]));

    await TestBed.configureTestingModule({
      imports: [MeasureMappingFormComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [
        { provide: MeasureMappingService, useValue: measureMappingService },
        { provide: MeasureDefinitionService, useValue: measureDefinitionService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MeasureMappingFormComponent);
    component = fixture.componentInstance;
  });

  function initWith(item: IMeasureMapping | undefined, formMode: FormMode): void {
    component.item = item as IMeasureMapping;
    component.formMode = formMode;
    fixture.detectChanges();
  }

  it('populates the fields from the item in Edit mode', () => {
    initWith(ach, FormMode.Edit);

    expect(component.measure.value).toBe('ACH');
    expect(component.dqm.value).toBe('NHSNAcuteCareHospitalDailyInitialPopulation');
    expect(component.frequency.value).toBe(Frequency.Monthly);
  });

  it('requires measure, dQM and frequency', () => {
    initWith(undefined, FormMode.Create);

    expect(component.measureMappingForm.valid).toBeFalse();

    component.measure.setValue('ACH');
    component.dqm.setValue('dqm-1');
    component.frequency.setValue(Frequency.Daily);

    expect(component.measureMappingForm.valid).toBeTrue();
  });

  it('rejects a measure or dQM longer than 255 characters', () => {
    initWith(ach, FormMode.Edit);

    component.measure.setValue('a'.repeat(256));
    expect(component.measure.valid).toBeFalse();

    component.measure.setValue('ACH');
    component.dqm.setValue('a'.repeat(256));
    expect(component.dqm.valid).toBeFalse();
  });

  it('does not call the service when the form is invalid', () => {
    initWith(undefined, FormMode.Create);

    component.measure.setValue('ACH');
    component.submitConfiguration();

    expect(measureMappingService.createMeasureMapping).not.toHaveBeenCalled();
  });

  it('creates with the entered fields in Create mode', () => {
    measureMappingService.createMeasureMapping.and.returnValue(of({} as IMeasureMapping));
    initWith(undefined, FormMode.Create);

    component.measure.setValue('ACH');
    component.dqm.setValue('dqm-1');
    component.frequency.setValue(Frequency.Weekly);
    component.submitConfiguration();

    expect(measureMappingService.createMeasureMapping).toHaveBeenCalledWith(
      jasmine.objectContaining({ measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Weekly }));
    expect(measureMappingService.updateMeasureMapping).not.toHaveBeenCalled();
  });

  it('updates the existing item in Edit mode', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(of({} as IMeasureMapping));
    initWith(ach, FormMode.Edit);

    component.frequency.setValue(Frequency.Monthly);
    component.submitConfiguration();

    expect(measureMappingService.updateMeasureMapping).toHaveBeenCalledWith(
      jasmine.objectContaining({ id: 'mm-1', measure: 'ACH', frequency: Frequency.Monthly }));
    expect(measureMappingService.createMeasureMapping).not.toHaveBeenCalled();
  });

  it('only offers the daily, weekly and monthly cadences', () => {
    initWith(undefined, FormMode.Create);

    expect(component.frequencyOptions).toEqual([Frequency.Daily, Frequency.Weekly, Frequency.Monthly]);
  });

  it('offers the MeasureEval measure definitions as dQM options, sorted', () => {
    initWith(undefined, FormMode.Create);

    expect(component.dqmOptions).toEqual([
      'NHSNAcuteCareHospitalDailyInitialPopulation',
      'NHSNGlycemicControlHypoglycemicInitialPopulation'
    ]);
  });

  it('keeps a saved dQM visible but invalid when its definition no longer exists', () => {
    const orphaned: IMeasureMapping = { ...ach, dqm: 'RetiredMeasure' };
    initWith(orphaned, FormMode.Edit);

    expect(component.dqmOptions![0]).toBe('RetiredMeasure');
    expect(component.dqm.value).toBe('RetiredMeasure');
    expect(component.dqmOptionLabel('RetiredMeasure')).toBe('RetiredMeasure (not found in MeasureEval)');
    expect(component.dqmOptionLabel('NHSNAcuteCareHospitalDailyInitialPopulation'))
      .toBe('NHSNAcuteCareHospitalDailyInitialPopulation');

    expect(component.dqm.hasError('unknownDqm')).toBeTrue();
    expect(component.measureMappingForm.valid).toBeFalse();

    component.submitConfiguration();
    expect(measureMappingService.updateMeasureMapping).not.toHaveBeenCalled();
  });

  it('becomes valid again once a current definition replaces the stale dQM', () => {
    const orphaned: IMeasureMapping = { ...ach, dqm: 'RetiredMeasure' };
    initWith(orphaned, FormMode.Edit);

    component.dqm.setValue('NHSNAcuteCareHospitalDailyInitialPopulation');

    expect(component.dqm.hasError('unknownDqm')).toBeFalse();
    expect(component.measureMappingForm.valid).toBeTrue();
  });

  it('does not flag a saved dQM that still exists', () => {
    initWith(ach, FormMode.Edit);

    expect(component.staleDqm).toBeNull();
    expect(component.dqm.hasError('unknownDqm')).toBeFalse();
    expect(component.measureMappingForm.valid).toBeTrue();
  });

  it('treats the 204 empty-collection response as no options', () => {
    measureDefinitionService.getMeasureDefinitionConfigurations.and.returnValue(
      of(null as unknown as IMeasureDefinitionConfigModel[]));
    initWith(undefined, FormMode.Create);

    expect(component.dqmOptions).toEqual([]);
  });

  it('falls back to free-text dQM entry when the definitions fetch fails', () => {
    measureDefinitionService.getMeasureDefinitionConfigurations.and.returnValue(throwError(() => new Error('down')));
    initWith(ach, FormMode.Edit);

    expect(component.dqmOptions).toBeNull();
    const input = fixture.nativeElement.querySelector('[data-testid="measure-mapping-dqm-input"]');
    expect(input).toBeTruthy();
  });

  it('emits success after a saved update', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(of({} as IMeasureMapping));
    initWith(ach, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes).toEqual([{ success: true, message: '' }]);
  });

  it('surfaces the duplicate-mapping validation error', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(throwError(() => ({
      error: { errors: { measure: ['A measure mapping for this measure and dQM already exists.'] } }
    })));
    initWith(ach, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toContain('already exists');
  });

  it('surfaces the plain-text unknown-dQM error', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(throwError(() => ({
      error: "DQM 'bogus-dqm' was not found in MeasureEval."
    })));
    initWith(ach, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toBe("DQM 'bogus-dqm' was not found in MeasureEval.");
  });

  it('surfaces the ProblemDetails message when MeasureEval is unreachable', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(throwError(() => ({
      error: { detail: 'Unable to verify the DQM in MeasureEval.' }
    })));
    initWith(ach, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toBe('Unable to verify the DQM in MeasureEval.');
  });

  it('falls back to a generic message when the error has no known shape', () => {
    measureMappingService.updateMeasureMapping.and.returnValue(throwError(() => new Error('boom')));
    initWith(ach, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toBe('boom');
  });
});
