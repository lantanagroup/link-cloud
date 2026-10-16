import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FormMode } from '../../../models/FormMode.enum';
import { IApiResponse } from '../../../interfaces/api-response.interface';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { MeasureDefinitionService } from '../../../services/gateway/measure-definition/measure.service';
import { IMeasureMapping, MEASURE_MAPPING_FREQUENCIES } from '../../../interfaces/dmrp/measure-mapping.interface';

@Component({
  selector: 'app-measure-mapping-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule
  ],
  templateUrl: './measure-mapping-form.component.html',
  styleUrls: ['./measure-mapping-form.component.scss']
})
export class MeasureMappingFormComponent implements OnInit {
  @Input() item!: IMeasureMapping;
  @Input() formMode!: FormMode;

  @Input() viewOnly: boolean = false;

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IApiResponse>();

  readonly frequencyOptions = MEASURE_MAPPING_FREQUENCIES;

  /**
   * dQM ids offered by the select, fetched from MeasureEval's measure definitions. Null until
   * the fetch resolves; the template falls back to the free-text input while null (still
   * loading, or the fetch failed) so the dialog never blocks a save on MeasureEval being down —
   * the backend validates unknown dQMs anyway.
   */
  dqmOptions: string[] | null = null;

  /**
   * A saved dQM whose definition the fetch did not return. It stays in the options so the
   * admin can see what the mapping pointed at, but it is labelled as missing and fails
   * validation, so the mapping cannot be re-saved until a real definition is chosen.
   */
  staleDqm: string | null = null;

  measureMappingForm!: FormGroup;

  constructor(
    private measureMappingService: MeasureMappingService,
    private measureDefinitionService: MeasureDefinitionService,
    private fb: FormBuilder
  ) {
    this.measureMappingForm = this.fb.group({
      measure: ['', [Validators.required, Validators.maxLength(255)]],
      dqm: ['', [Validators.required, Validators.maxLength(255), this.knownDqmValidator.bind(this)]],
      frequency: [null, Validators.required]
    });
  }

  get measure() {
    return this.measureMappingForm.controls['measure'];
  }

  get dqm() {
    return this.measureMappingForm.controls['dqm'];
  }

  get frequency() {
    return this.measureMappingForm.controls['frequency'];
  }

  ngOnInit(): void {
    this.measureMappingForm.reset();

    if (this.item) {
      this.measure.setValue(this.item.measure ?? '');
      this.dqm.setValue(this.item.dqm ?? '');
      this.frequency.setValue(this.item.frequency ?? null);
    }

    this.measureMappingForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.measureMappingForm.invalid);
    });

    this.loadDqmOptions();
  }

  private loadDqmOptions(): void {
    this.measureDefinitionService.getMeasureDefinitionConfigurations().subscribe({
      next: (definitions) => {
        // The BFF answers an empty collection with 204 and no body, so definitions can be null.
        const ids = (definitions ?? []).map(d => d.id).sort((a, b) => a.localeCompare(b));

        // Keep a saved dQM visible even when its definition no longer exists in MeasureEval,
        // so opening an old mapping doesn't silently blank the field. knownDqmValidator keeps
        // the form invalid until a real definition replaces it.
        const current = this.dqm.value;
        this.staleDqm = current && !ids.includes(current) ? current : null;
        if (this.staleDqm) {
          ids.unshift(this.staleDqm);
        }

        this.dqmOptions = ids;

        // The value didn't change, so revalidate explicitly and surface the result: the error
        // should show without waiting for a touch, and the dialog's save-disabled state tracks
        // formValueChanged.
        this.dqm.updateValueAndValidity();
        if (this.staleDqm) {
          this.dqm.markAsTouched();
        }
        this.formValueChanged.emit(this.measureMappingForm.invalid);
      },
      error: () => {
        // Leave dqmOptions null: the free-text input stays, and ErrorHandlingService has
        // already surfaced the fetch failure. Still emit, or a pre-filled valid form keeps
        // the dialog's save button disabled until the admin edits a field.
        this.formValueChanged.emit(this.measureMappingForm.invalid);
      }
    });
  }

  /** Labels the stale entry so the admin can tell it apart from real definitions. */
  dqmOptionLabel(dqmId: string): string {
    return dqmId === this.staleDqm ? `${dqmId} (not found in MeasureEval)` : dqmId;
  }

  /**
   * Invalid while the selected dQM is the stale one. Free-text mode (definitions not loaded)
   * stays valid here — the backend rejects unknown dQMs on save either way.
   */
  private knownDqmValidator(control: AbstractControl): ValidationErrors | null {
    if (this.staleDqm !== null && control.value === this.staleDqm) {
      return { unknownDqm: true };
    }
    return null;
  }

  submitConfiguration(): void {
    if (this.measureMappingForm.status != 'VALID') {
      return;
    }

    const submitted: IMeasureMapping = {
      ...this.item,
      measure: this.measure.value,
      dqm: this.dqm.value,
      frequency: this.frequency.value
    };

    const save = this.formMode == FormMode.Create
      ? this.measureMappingService.createMeasureMapping(submitted)
      : this.measureMappingService.updateMeasureMapping(submitted);

    save.subscribe({
      next: () => {
        this.submittedConfiguration.emit({ success: true, message: '' });
      },
      error: (err) => {
        this.submittedConfiguration.emit({ success: false, message: this.failureMessage(err) });
      }
    });
  }

  /**
   * The controller reports the same failure three different ways: a ValidationProblem for a
   * duplicate measure/dQM pair (err.error.errors.measure[]), a plain-text 400 when the dQM isn't
   * known to MeasureEval (err.error is the string itself), and a ProblemDetails 502 when
   * MeasureEval can't be reached (err.error.detail). Cover all three before falling back to the
   * generic HTTP error message.
   */
  private failureMessage(err: any): string {
    const fieldMessages = Object.values(err?.error?.errors ?? {}).flat() as string[];
    if (fieldMessages.length) {
      return fieldMessages.join(' ');
    }

    if (typeof err?.error === 'string' && err.error.trim()) {
      return err.error;
    }

    if (typeof err?.error?.detail === 'string' && err.error.detail.trim()) {
      return err.error.detail;
    }

    return err?.message ?? 'Failed to save the measure mapping. Please try again.';
  }
}
