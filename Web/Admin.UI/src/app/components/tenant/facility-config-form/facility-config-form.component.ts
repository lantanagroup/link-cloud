import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';

import {
    AbstractControl,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {IFacilityConfigModel} from 'src/app/interfaces/tenant/facility-config-model.interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { FormMode } from 'src/app/models/FormMode.enum';
import { ENTER, COMMA } from '@angular/cdk/keycodes';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MatToolbarModule } from '@angular/material/toolbar';
import {MatSelectModule} from "@angular/material/select";
import {MatCardModule} from "@angular/material/card";
import {MatTabsModule} from "@angular/material/tabs";
import {MatExpansionModule} from "@angular/material/expansion";
import {MatProgressSpinnerModule} from "@angular/material/progress-spinner";
import * as moment from 'moment-timezone';
import {ScheduledReportsValidator} from "../../validators/ScheduledReportsValidator";
import {MeasureDefinitionService} from "../../../services/gateway/measure-definition/measure.service";
import { AppConfig, AppConfigService } from '../../../services/app-config.service';

export function facilityIdConditionalValidator(allowAlphaNumeric: boolean): ValidatorFn {
  return (control: AbstractControl) => {
    const value = control.value;
    if (allowAlphaNumeric) {
      const valid = /^[a-zA-Z0-9-]+$/.test(value);
      return valid ? null : { invalidFacilityId: 'Facility ID must be alphanumeric plus hyphens only.' };
    } else {
      const valid = /^[0-9]{5}$/.test(value);
      return valid ? null : { invalidFacilityId: 'Facility ID must be a valid NHSN ORG ID (5 numbers).' };
    }
  };
}

@Component({
  selector: 'app-facility-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSnackBarModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatToolbarModule,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    MatProgressSpinnerModule
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

  timezones: string[] = moment.tz.names();

  formMode!: FormMode;
  facilityConfigForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  reportTypes: string[] = [];

  appConfig?: AppConfig;

  constructor(
    private snackBar: MatSnackBar,
    private tenantService: TenantService,
    private measureDefinitionConfigurationService: MeasureDefinitionService,
    private appConfigService: AppConfigService) { }

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  async ngOnInit(): Promise<void> {


    this.appConfig = await this.appConfigService.loadConfig();

    this.facilityConfigForm = new FormGroup(
      {
        facilityId: new FormControl('', [Validators.required, facilityIdConditionalValidator(this.appConfig?.allowAlphaNumericFacilityId ?? true)]),
        facilityName: new FormControl('', Validators.required),
        timeZone: new FormControl('', Validators.required), // Add timezone control
        monthlyReports: new FormControl([]),
        dailyReports: new FormControl([]),
        weeklyReports: new FormControl([]),
      },
      { validators: ScheduledReportsValidator() } // Apply the custom validator to the entire FormGroup
    );

    this.measureDefinitionConfigurationService.getMeasureDefinitionConfigurations().subscribe(
      {
        next: (response) => {
          this.reportTypes = response.map(model => model.id);
        },
        error: (err) => {
          this.submittedConfiguration.emit({id: '', message: err.message});
        }
      });

    if(this.item) {
      this.formMode = FormMode.Edit;

      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.facilityNameControl.setValue(this.item.facilityName);
      this.facilityNameControl.updateValueAndValidity();

      this.timeZoneControl.setValue(this.item.timeZone);
      this.timeZoneControl.updateValueAndValidity();

      this.monthlyReportsControl.setValue(this.item.scheduledReports.monthly);
      this.monthlyReportsControl.updateValueAndValidity();

      this.weeklyReportsControl.setValue(this.item.scheduledReports.weekly);
      this.weeklyReportsControl.updateValueAndValidity();

      this.dailyReportsControl.setValue(this.item.scheduledReports.daily);
      this.dailyReportsControl.updateValueAndValidity();

    }
    else {
      this.formMode = FormMode.Create;
    }

    // toggle view
    this.toggleViewOnly(this.viewOnly);

    this.facilityConfigForm.valueChanges.subscribe(() => {
      this.facilityConfigForm.updateValueAndValidity();
      this.formValueChanged.emit(this.facilityConfigForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.facilityNameControl.setValue(this.item.facilityName);
      this.facilityNameControl.updateValueAndValidity();

      this.timeZoneControl.setValue(this.item.timeZone);
      this.timeZoneControl.updateValueAndValidity();

      this.monthlyReportsControl.setValue(this.item.scheduledReports.monthly)
      this.monthlyReportsControl.updateValueAndValidity();

      this.weeklyReportsControl.setValue(this.item.scheduledReports.weekly);
      this.weeklyReportsControl.updateValueAndValidity();

      this.dailyReportsControl.setValue(this.item.scheduledReports.daily);
      this.dailyReportsControl.updateValueAndValidity();

      // toggle view
      this.toggleViewOnly(this.viewOnly);
    }
  }

  toggleViewOnly(viewOnly: boolean) {

    if (viewOnly) {
      this.facilityIdControl.disable();
      this.facilityNameControl.disable();
      this.timeZoneControl.disable();
      this.monthlyReportsControl.disable();
      this.weeklyReportsControl.disable();
      this.dailyReportsControl.disable();
    } else {
      this.facilityIdControl.enable();
      this.facilityNameControl.enable();
      this.timeZoneControl.enable();
      this.monthlyReportsControl.enable();
      this.weeklyReportsControl.enable();
      this.dailyReportsControl.enable();
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

  get timeZoneControl(): FormControl {
    return this.facilityConfigForm.get('timeZone') as FormControl;
  }

  get monthlyReportsControl(): FormControl {
    return this.facilityConfigForm.get('monthlyReports') as FormControl;
  }

  get weeklyReportsControl(): FormControl {
    return this.facilityConfigForm.get('weeklyReports') as FormControl;
  }

  get dailyReportsControl(): FormControl {
    return this.facilityConfigForm.get('dailyReports') as FormControl;
  }

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  clearFacilityName(): void {
    this.facilityNameControl.setValue('');
    this.facilityNameControl.updateValueAndValidity();
  }

  get noReportsEntered(): string | null {
    return this.facilityConfigForm.errors?.['noReportsEntered'] || null;
  }

  get reportsNotUniqueError(): string | null {
    return this.facilityConfigForm.errors?.['reportsNotUnique'] || null;
  }

  submitConfiguration(): void {
    if(this.facilityConfigForm.valid) {

      let monthlyReports : string[] = this.monthlyReportsControl.value ?? [];
      let weeklyReports : string[] = this.weeklyReportsControl.value ?? [];
      let dailyReports : string[] = this.dailyReportsControl.value ?? [];
      let scheduledReports: { daily: string[], monthly: string[], weekly: string[] } = {"daily": dailyReports, "monthly": monthlyReports, "weekly": weeklyReports};

      if(this.formMode == FormMode.Create) {
        this.tenantService.createFacility(this.facilityIdControl.value, this.facilityNameControl.value, this.timeZoneControl.value, scheduledReports).subscribe({
          next: (response) => {
            if (response) {
              this.submittedConfiguration.emit({id: response.id, message: "Facility Created"});
            }
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: "", message: err.message});
          }
        });
      }
      else if(this.formMode == FormMode.Edit) {
        this.tenantService.updateFacility(this.item.id ?? '', this.facilityIdControl.value, this.facilityNameControl.value, this.timeZoneControl.value, scheduledReports).subscribe( {
          next: (response) => {
            this.submittedConfiguration.emit({id:  this.item.id ?? '', message: "Facility Updated"});
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: this.item.id ?? '', message: err.message});
          }
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
}
