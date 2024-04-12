import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IFacilityConfigModel, IScheduledTaskModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { FormMode } from 'src/app/models/FormMode.enum';
import { ENTER, COMMA } from '@angular/cdk/keycodes';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MatToolbarModule } from '@angular/material/toolbar';

@Component({
  selector: 'app-facility-config-form',
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
  templateUrl: './facility-config-form.component.html',
  styleUrls: ['./facility-config-form.component.scss']
})
export class FacilityConfigFormComponent implements OnInit, OnChanges {

  @Input() item!: IFacilityConfigModel;  

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  formMode!: FormMode;
  facilityConfigForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  constructor(private snackBar: MatSnackBar, private tenantService: TenantService) { 

    //initialize form with fields based on IFacilityConfigModel
    this.facilityConfigForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      facilityName: new FormControl('', Validators.required),
      scheduledTasks: new FormArray([])
    }); 

  }

  ngOnInit(): void {
    this.facilityConfigForm.reset();

    if(this.item) {
      this.formMode = FormMode.Edit;

      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.facilityNameControl.setValue(this.item.facilityName);     
      this.facilityNameControl.updateValueAndValidity();

      this.loadScheduledTasks(this.item.scheduledTasks);
      this.scheduledTasks.updateValueAndValidity();
    }
    else {
      this.formMode = FormMode.Create;
    }   

    this.facilityConfigForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.facilityConfigForm.invalid);
    });

  }

  ngOnChanges(changes: SimpleChanges) {     
    
    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.facilityNameControl.setValue(this.item.facilityName);     
      this.facilityNameControl.updateValueAndValidity();

      this.loadScheduledTasks(this.item.scheduledTasks);
      this.scheduledTasks.updateValueAndValidity();     
    }
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  //form control getters
  get facilityIdControl(): FormControl {
    return this.facilityConfigForm.get('facilityId') as FormControl;
  }

  get facilityNameControl(): FormControl {
    return this.facilityConfigForm.get('facilityName') as FormControl;
  }

  get scheduledTasks(): FormArray {
    return this.facilityConfigForm.get('scheduledTasks') as FormArray;
  }

  get reportTypeSchedulesForms(): FormArray {
    return this.scheduledTasks.get('reportTypeSchedules') as FormArray;
  }  

  addScheduledTask(): void {
    this.scheduledTasks.push(new FormGroup({
      kafkaTopic: new FormControl('', Validators.required),
      reportTypeSchedules: new FormArray([
        new FormGroup({
          reportType: new FormControl('', Validators.required),
          scheduledTriggers: new FormArray([
            new FormGroup({ trigger: new FormControl('', Validators.required) })
          ])
        })
      ])
    }));

    this.scheduledTasks.updateValueAndValidity();
  }  

  deleteScheduledTask(taskIndex: number): void {
    this.scheduledTasks.removeAt(taskIndex);
  }

  getReportTypeSchedules(taskIndex: number) {
    return (this.scheduledTasks.at(taskIndex) as FormGroup).get('reportTypeSchedules') as FormArray;
  }

  addReportTypeSchedule(taskIndex: number): void {
    const reportTypeSchedules = this.getReportTypeSchedules(taskIndex);
    reportTypeSchedules.push(new FormGroup({
      reportType: new FormControl('', Validators.required),
      scheduledTriggers: new FormArray([
        new FormGroup({trigger: new FormControl('', Validators.required)})
      ])
    }));

    reportTypeSchedules.updateValueAndValidity();
  }  
  
  deleteReportTypeSchedule(taskIndex: number, scheduleIndex: number): void {
    const reportTypeSchedules = this.scheduledTasks.at(taskIndex).get('reportTypeSchedules') as FormArray;
    reportTypeSchedules.removeAt(scheduleIndex);
  }

  getScheduledTriggers(taskIndex: number, scheduleIndex: number): FormArray {
    return (this.getReportTypeSchedules(taskIndex).at(scheduleIndex) as FormGroup).get('scheduledTriggers') as FormArray;
  }

  addScheduledTrigger(taskIndex: number, scheduleIndex: number): void {
    const scheduledTriggers = this.getReportTypeSchedules(taskIndex).at(scheduleIndex).get('scheduledTriggers') as FormArray;
    scheduledTriggers.push(new FormGroup({trigger: new FormControl('', Validators.required)}));
  }

  deleteScheduledTrigger(taskIndex: number, scheduleIndex: number, triggerIndex: number): void {
    const scheduledTriggers = this.getScheduledTriggers(taskIndex, scheduleIndex) as FormArray;
    scheduledTriggers.removeAt(triggerIndex);
  }

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  clearFacilityName(): void {
    this.facilityNameControl.setValue('');
    this.facilityNameControl.updateValueAndValidity();
  }

  clearScheduledTasks(): void {
    this.scheduledTasks.clear();
    this.scheduledTasks.updateValueAndValidity();
  }

  removeScheduledTask(index: number): void {
    this.scheduledTasks.removeAt(index);
    this.scheduledTasks.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if(this.facilityConfigForm.valid) {
      if(this.formMode == FormMode.Create) {
        let scheduledTasks: IScheduledTaskModel[] = this.mapScheduledTasks();
        this.tenantService.createFacility(this.facilityIdControl.value ?? '', this.facilityNameControl.value, scheduledTasks).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
      else if(this.formMode == FormMode.Edit) {
        let scheduledTasks: IScheduledTaskModel[] = this.mapScheduledTasks();
        this.tenantService.updateFacility(this.item.id ?? '', this.facilityIdControl.value ?? '', this.facilityNameControl.value, scheduledTasks).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
    }
    else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  private mapScheduledTasks(): IScheduledTaskModel[] {
    return this.scheduledTasks.value.map((task: any) => {
      return {
        kafkaTopic: task.kafkaTopic,
        reportTypeSchedules: task.reportTypeSchedules.map((schedule: any) => {
          return {
            reportType: schedule.reportType,
            scheduledTriggers: schedule.scheduledTriggers.map((trigger: any) => {
              return trigger.trigger;
            })
          }
        })
      }
    });
  }

  private loadScheduledTasks(scheduledTasks: IScheduledTaskModel[]): void {

    this.scheduledTasks.clear();
    this.scheduledTasks.updateValueAndValidity();

    scheduledTasks.forEach((task: IScheduledTaskModel) => {
      this.scheduledTasks.push(new FormGroup({
        kafkaTopic: new FormControl(task.kafkaTopic, Validators.required),
        reportTypeSchedules: new FormArray(
          task.reportTypeSchedules.map((schedule: any) => {
            return new FormGroup({
              reportType: new FormControl(schedule.reportType, Validators.required),
              scheduledTriggers: new FormArray(
                schedule.scheduledTriggers.map((trigger: any) => {
                  return new FormGroup({ trigger: new FormControl(trigger, Validators.required) });
                })
              )
            })
          })
        )
      }));
    });
  }

}
