import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FormMode } from '../../../models/FormMode.enum';
import { IApiResponse } from '../../../interfaces/api-response.interface';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { Frequency, IMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

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

  private _viewOnly: boolean = false;

  @Input()
  set viewOnly(v: boolean) {
    if (v) this._viewOnly = v;
  }

  get viewOnly() {
    return this._viewOnly;
  }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IApiResponse>();

  readonly frequencyOptions = Object.values(Frequency);

  measureMappingForm!: FormGroup;

  constructor(private measureMappingService: MeasureMappingService, private fb: FormBuilder) {
    this.measureMappingForm = this.fb.group({
      measure: ['', [Validators.required, Validators.maxLength(255)]],
      dqm: ['', [Validators.required, Validators.maxLength(255)]],
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
