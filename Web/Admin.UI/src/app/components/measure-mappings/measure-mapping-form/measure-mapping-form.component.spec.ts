import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { MeasureMappingFormComponent } from './measure-mapping-form.component';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { FormMode } from '../../../models/FormMode.enum';
import { Frequency, IMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

describe('MeasureMappingFormComponent', () => {
  let component: MeasureMappingFormComponent;
  let fixture: ComponentFixture<MeasureMappingFormComponent>;
  let measureMappingService: jasmine.SpyObj<MeasureMappingService>;

  const ach: IMeasureMapping = { id: 'mm-1', measure: 'ACH', dqm: 'NHSNAcuteCareHospitalDailyInitialPopulation', frequency: Frequency.Monthly };

  beforeEach(async () => {
    measureMappingService = jasmine.createSpyObj<MeasureMappingService>('MeasureMappingService', ['createMeasureMapping', 'updateMeasureMapping']);

    await TestBed.configureTestingModule({
      imports: [MeasureMappingFormComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [{ provide: MeasureMappingService, useValue: measureMappingService }]
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

    component.frequency.setValue(Frequency.Adhoc);
    component.submitConfiguration();

    expect(measureMappingService.updateMeasureMapping).toHaveBeenCalledWith(
      jasmine.objectContaining({ id: 'mm-1', measure: 'ACH', frequency: Frequency.Adhoc }));
    expect(measureMappingService.createMeasureMapping).not.toHaveBeenCalled();
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
