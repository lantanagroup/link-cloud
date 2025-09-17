import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatToolbarModule} from '@angular/material/toolbar';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {ENTER, COMMA} from '@angular/cdk/keycodes';
import {FormMode} from 'src/app/models/FormMode.enum';
import {QueryDispatchService} from "../../../services/gateway/query-dispatch/query-dispatch.service";
import {IQueryDispatchConfiguration} from "../../../interfaces/query-dispatch/query-dispatch-config-model.interface";


@Component({
  selector: 'app-query-dispatch-config-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule
  ],
  templateUrl: './query-dispatch-config-form.component.html',
  styleUrls: ['./query-dispatch-config-form.component.scss']
})
export class QueryDispatchConfigFormComponent implements OnInit, OnChanges {

  @Input() item!: IQueryDispatchConfiguration;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) {
    if (v !== null) this._viewOnly = v;
  }

  get viewOnly() {
    return this._viewOnly;
  }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  queryDispatchForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;
  iso8601DurationRegex = /^P(?!$)(\d+Y)?(\d+M)?(\d+D)?(T(?=\d)(\d+H)?(\d+M)?(\d+S)?)?$/;

  constructor(private snackBar: MatSnackBar, private queryDispatchService: QueryDispatchService, private fb: FormBuilder) {

    this.queryDispatchForm = this.fb.group({
      facilityId: ['', Validators.required],
      dispatchSchedules: this.fb.array([this.createSchedule()])
    });
  }

  ngOnInit(): void {
    this.queryDispatchForm.reset();
    this.setSchedulesDisabled(this.viewOnly);
    if (this.item) {
      // set facilityId
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      // set dispatchSchedules
      this.setDispatchSchedules(this.item.dispatchSchedules);
    }

    this.queryDispatchForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.queryDispatchForm.invalid);
    });
  }

  setSchedulesDisabled(viewOnly: boolean) {
    this.dispatchSchedules.controls.forEach(schedule => {
      schedule.get('event')?.disable();
      if (viewOnly) {
        schedule.get('duration')?.disable();
      } else {
        schedule.get('duration')?.enable();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    // set dispatchSchedules
    this.setDispatchSchedules(this.item.dispatchSchedules);
    if (changes['item'] && changes['item'].currentValue) {
      this.setSchedulesDisabled(this.viewOnly);
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();
      // toggle view
      this.toggleViewOnly(this.viewOnly);
    }
  }

  private setDispatchSchedules(schedules?: { event: string; duration: string }[]) {
    this.dispatchSchedules.clear();
    if (schedules && schedules.length) {
      schedules.forEach(schedule => {
        this.dispatchSchedules.push(this.fb.group({
          event: [{value: schedule.event || 'Discharge', disabled: true}, Validators.required],
          duration: [schedule.duration, [Validators.required, Validators.pattern(this.iso8601DurationRegex)]]
        }));
      });
    } else {
      // ensure at least one empty schedule row
      this.dispatchSchedules.push(this.createSchedule());
    }
  }

  get facilityIdControl(): FormControl {
    return this.queryDispatchForm.get('facilityId') as FormControl;
  }

  get dispatchSchedules(): FormArray {
    return this.queryDispatchForm.get('dispatchSchedules') as FormArray;
  }

  private createSchedule(): FormGroup {
    return this.fb.group({
      event: [{value: 'Discharge', disabled: true}, Validators.required],
      duration: ['', [Validators.required, Validators.pattern(this.iso8601DurationRegex)]]
    });
  }

  // Dynamically disable or enable the form control based on viewOnly
  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
  }

  submitConfiguration(): void {
    if (this.queryDispatchForm.valid) {
      if (this.formMode == FormMode.Create) {
        this.queryDispatchService.createConfiguration(this.facilityIdControl.value, this.dispatchSchedules.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: '', message: "Query Dispatch Created"});
        });
      } else if (this.formMode == FormMode.Edit) {
        this.queryDispatchService.updateConfiguration(this.facilityIdControl.value, this.dispatchSchedules.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: '', message: "Query Dispatch Updated"});
        });
      }
    } else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }
}
