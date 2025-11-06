import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {CommonModule, NgForOf, NgIf} from '@angular/common';
import {
  AbstractControlOptions,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule, ValidationErrors,
  Validators
} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatToolbarModule} from '@angular/material/toolbar';
import {
  IDataAcquisitionQueryConfigModel
} from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface';
import {FormMode} from 'src/app/models/FormMode.enum';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {ENTER, COMMA} from '@angular/cdk/keycodes';
import {DataAcquisitionService} from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {MatSelectModule} from "@angular/material/select";
import {MatCheckboxModule} from "@angular/material/checkbox";

@Component({
  selector: 'app-data-acquisition-fhir-query-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    MatSelectModule,
    FormsModule,
    MatCheckboxModule,
    NgForOf,
    NgIf
  ],
  templateUrl: './data-acquisition-fhir-query-config-form.component.html',
  styleUrls: ['./data-acquisition-fhir-query-config-form.component.scss']
})
export class DataAcquisitionFhirQueryConfigFormComponent implements OnInit, OnChanges{
  @Input() item!: IDataAcquisitionQueryConfigModel;

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

  configForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  authTypes: string[] = ["Basic", "Epic", "None"];

  hoursOptions = [null, ...Array.from({ length: 24 }, (_, i) => i)]; // 0..23    // 0..23
  minutesOptions = [0, ...Array.from({ length: 59 }, (_, i) => i + 1)];
  secondsOptions = [0, ...Array.from({ length: 59 }, (_, i) => i + 1)];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.configForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      fhirServerBaseUrl: new FormControl('', Validators.required),
      isAuthEnabled: new FormControl(false),

      maxConcurrentRequests: new FormControl(1, [Validators.required, Validators.min(1), Validators.max(16)]),

      // Min acquisition pull time
      minAcqPull: this.createTimeGroup(),

      // Max acquisition pull time
      maxAcqPull: this.createTimeGroup(),

      authType: new FormControl(''),
      authKey: new FormControl(''),
      tokenUrl: new FormControl(''),
      audience: new FormControl(''),
      clientId: new FormControl(''),
      userName: new FormControl(''),
      password: new FormControl('')
    },  { validators: this.bothOrNoneHoursValidator } as AbstractControlOptions);
  }

  // Helper function
  private createTimeGroup(defaultMinutes = 0, defaultSeconds = 0): FormGroup {
    return new FormGroup({
      hours: new FormControl(null),
      minutes: new FormControl(defaultMinutes),
      seconds: new FormControl(defaultSeconds)
    });
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {

      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirServerBaseUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.setMinAcqPull(this.item.minAcquisitionPullTime ?? "");
      this.setMaxAcqPull(this.item.maxAcquisitionPullTime ?? "");
      if (this.item.maxConcurrentRequests != null) {
        this.maxConcurrentRequestsControl.setValue(this.item.maxConcurrentRequests);
      }

      this.isAuthEnabledControl.setValue(!!this.item.authentication?.authType);
      this.isAuthEnabledControl.updateValueAndValidity();

      this.authTypeControl.setValue(this.item.authentication?.authType);
      this.authTypeControl.updateValueAndValidity();

      this.authKeyControl.setValue(this.item.authentication?.key);
      this.authKeyControl.updateValueAndValidity();

      this.tokenUrlControl.setValue(this.item.authentication?.tokenUrl);
      this.tokenUrlControl.updateValueAndValidity();

      this.audienceControl.setValue(this.item.authentication?.audience);
      this.audienceControl.updateValueAndValidity();

      this.clientIdControl.setValue(this.item.authentication?.clientId);
      this.clientIdControl.updateValueAndValidity();

      this.userNameControl.setValue(this.item.authentication?.userName);
      this.userNameControl.updateValueAndValidity();

      this.passwordControl.setValue(this.item.authentication?.password);
      this.passwordControl.updateValueAndValidity();
    }

    this.authTypeControl?.valueChanges.subscribe((value) => {
      this.updateValidators(value);
    });

    if (this.authTypeControl?.value) {
      this.updateValidators(this.authTypeControl?.value);
    }

    this.minAcqHoursControl.valueChanges.subscribe(value => {
      if (value != null) {
        this.minAcqMinutesControl.enable();
        this.minAcqSecondsControl.enable();
      } else {
        this.minAcqMinutesControl.setValue(0);
        this.minAcqSecondsControl.setValue(0);
        this.minAcqMinutesControl.disable();
        this.minAcqSecondsControl.disable();
      }
      //this.configForm.updateValueAndValidity({ onlySelf: false });
    });

    this.maxAcqHoursControl.valueChanges.subscribe(value => {
      if (value != null) {
        this.maxAcqMinutesControl.enable();
        this.maxAcqSecondsControl.enable();
      } else {
        this.maxAcqMinutesControl.setValue(0);
        this.maxAcqSecondsControl.setValue(0);
        this.maxAcqMinutesControl.disable();
        this.maxAcqSecondsControl.disable();
      }
      //this.configForm.updateValueAndValidity({ onlySelf: false });
    });

    this.configForm.valueChanges.subscribe(() => {
      if (this.isAuthEnabledControl.value == true) {
        this.configForm.controls['authType'].setValidators(Validators.required);
      } else {
        this.configForm.controls['authType'].clearValidators();
        this.configForm.controls['authKey'].clearValidators();
        this.configForm.controls['tokenUrl'].clearValidators();
        this.configForm.controls['audience'].clearValidators();
        this.configForm.controls['clientId'].clearValidators();
        this.configForm.controls['userName'].clearValidators();
        this.configForm.controls['password'].clearValidators();
      }
      this.formValueChanged.emit(this.configForm.invalid || (this.isAuthEnabledControl.value && !this.authTypeControl.value) );
    });
  }


  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {

      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirServerBaseUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.setMinAcqPull(this.item.minAcquisitionPullTime ?? null);
      this.setMaxAcqPull(this.item.maxAcquisitionPullTime ?? null);
      this.maxConcurrentRequestsControl.setValue(this.item.maxConcurrentRequests);

      this.isAuthEnabledControl.setValue(!!this.item.authentication?.authType);
      this.isAuthEnabledControl.updateValueAndValidity();

      this.authTypeControl.setValue(this.item.authentication?.authType);
      this.authTypeControl.updateValueAndValidity();

      this.authKeyControl.setValue(this.item.authentication?.key);
      this.authKeyControl.updateValueAndValidity();

      this.tokenUrlControl.setValue(this.item.authentication?.tokenUrl);
      this.tokenUrlControl.updateValueAndValidity();

      this.audienceControl.setValue(this.item.authentication?.audience);
      this.audienceControl.updateValueAndValidity();

      this.clientIdControl.setValue(this.item.authentication?.clientId);
      this.clientIdControl.updateValueAndValidity();

      this.userNameControl.setValue(this.item.authentication?.userName);
      this.userNameControl.updateValueAndValidity();

      this.passwordControl.setValue(this.item.authentication?.password);
      this.passwordControl.updateValueAndValidity();

      // toggle view
      this.toggleViewOnly(this.viewOnly);
    }
  }

  compareNumbers = (a: number, b: number) => a === b;

  setMinAcqPull(time: string | null): void {
    if (!time) {
      // If null or empty, clear all fields
      this.minAcqHoursControl.setValue(null);
      this.minAcqMinutesControl.setValue(0);
      this.minAcqSecondsControl.setValue(0);
      return;
    }
    const { hour, minute, second } = this.parseTime(time);

    this.minAcqHoursControl.setValue(hour ?? null);
    this.minAcqMinutesControl.setValue(minute ?? 0);
    this.minAcqSecondsControl.setValue(second ?? 0);
  }

  getAcqPull(groupName: 'minAcqPull' | 'maxAcqPull'): string | null{
    const group = this.configForm.get(groupName) as FormGroup;
    const { hours, minutes, seconds } = group.value;

    // Return null if hours is not set (null, undefined, or empty string)
    if (hours == null || hours === '') {
      return null;
    }
    return `${(hours ?? 0).toString().padStart(2, '0')}:${(minutes ?? 0).toString().padStart(2, '0')}:${(seconds ?? 0).toString().padStart(2, '0')}.0000000`;
  }

  setMaxAcqPull(time: string | null): void {
    if (!time) {
      // If null or empty, clear all fields
      this.maxAcqHoursControl.setValue(null);
      this.maxAcqMinutesControl.setValue(0);
      this.maxAcqSecondsControl.setValue(0);
      return;
    }
    const { hour, minute, second } = this.parseTime(time);

    this.maxAcqHoursControl.setValue(hour ?? null);
    this.maxAcqMinutesControl.setValue(minute ?? 0);
    this.maxAcqSecondsControl.setValue(second ?? 0);
  }

  // getter for easier access in template
  get maxConcurrentRequestsControl(): FormControl {
    return this.configForm.get('maxConcurrentRequests') as FormControl;
  }

  private parseTime(time: string | null): { hour: number; minute: number; second: number } {
    if (!time) return { hour: 0, minute: 0, second: 0 };

    // Remove milliseconds if present
    const [h, m, sWithMs] = time.split(':');
    const s = sWithMs.split('.')[0]; // take only the part before the dot

    return {
      hour: Number(h),
      minute: Number(m),
      second: Number(s),
    };
  }

  private updateValidators(authType: string): void {
    const isAuthRequired = authType !== 'None' && authType !== 'Basic';
    const isBasicAuth = authType === 'Basic';

    // Manage validators for fields requiring authentication
    this.toggleValidators('authKey', isAuthRequired);
    this.toggleValidators('tokenUrl', isAuthRequired);
    this.toggleValidators('audience', isAuthRequired);
    this.toggleValidators('clientId', isAuthRequired);

    // Manage validators for Basic Auth fields
    this.toggleValidators('userName', isBasicAuth);
    this.toggleValidators('password', isBasicAuth);
  }

  private toggleValidators(controlName: string, shouldRequire: boolean): void {
    const control = this.configForm.controls[controlName];
    if (shouldRequire) {
      control.setValidators(Validators.required);
    } else {
      control.clearValidators();
    }
    control.updateValueAndValidity();
  }


  // Dynamically disable or enable the form control based on viewOnly
  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.fhirServerBaseUrlControl.disable();
      this.authTypeControl.disable();
      this.authKeyControl.disable();
      this.tokenUrlControl.disable();
      this.audienceControl.disable();
      this.clientIdControl.disable();
      this.userNameControl.disable();
      this.passwordControl.disable();
      this.maxConcurrentRequestsControl.disable();
      this.minAcqHoursControl.disable();
      this.minAcqMinutesControl.disable();
      this.minAcqSecondsControl.disable();
      this.maxAcqHoursControl.disable();
      this.maxAcqMinutesControl.disable();
      this.maxAcqSecondsControl.disable();
      this.isAuthEnabledControl.disable();
    } else {
      this.fhirServerBaseUrlControl.enable();
      this.authTypeControl.enable();
      this.authKeyControl.enable();
      this.tokenUrlControl.enable();
      this.audienceControl.enable();
      this.clientIdControl.enable();
      this.userNameControl.enable();
      this.passwordControl.enable();
      this.maxConcurrentRequestsControl.enable();
      this.minAcqHoursControl.enable();
      const enableMin = this.minAcqHoursControl.value !== null
      this.minAcqMinutesControl[enableMin ? 'enable' : 'disable']();
      this.minAcqSecondsControl[enableMin ? 'enable' : 'disable']();

      const enableMax = this.maxAcqHoursControl.value !== null;
      this.maxAcqMinutesControl[enableMax ? 'enable' : 'disable']();
      this.maxAcqSecondsControl[enableMax ? 'enable' : 'disable']();

      this.isAuthEnabledControl.enable();
    }
  }

  toggleAuthFields() {
    if (!this.isAuthEnabledControl.value) {
      this.configForm.controls['authType'].reset();
      this.configForm.controls['authKey'].reset();
      this.configForm.controls['tokenUrl'].reset();
      this.configForm.controls['audience'].reset();
      this.configForm.controls['clientId'].reset();
      this.configForm.controls['userName'].reset();
      this.configForm.controls['password'].reset();
    }
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get fhirServerBaseUrlControl(): FormControl {
    return this.configForm.get('fhirServerBaseUrl') as FormControl;
  }

  get isAuthEnabledControl(): FormControl {
    return this.configForm.get('isAuthEnabled') as FormControl;
  }

  get authTypeControl(): FormControl {
    return this.configForm.get('authType') as FormControl;
  }

  get authKeyControl(): FormControl {
    return this.configForm.get('authKey') as FormControl;
  }

  get minAcqHoursControl(): FormControl {
    return this.configForm.get('minAcqPull.hours') as FormControl;
  }

  get minAcqMinutesControl(): FormControl {
    return this.configForm.get('minAcqPull.minutes') as FormControl;
  }

  get minAcqSecondsControl(): FormControl {
    return this.configForm.get('minAcqPull.seconds') as FormControl;
  }

  get maxAcqHoursControl(): FormControl {
    return this.configForm.get('maxAcqPull.hours') as FormControl;
  }

  get maxAcqMinutesControl(): FormControl {
    return this.configForm.get('maxAcqPull.minutes') as FormControl;
  }

  get maxAcqSecondsControl(): FormControl {
    return this.configForm.get('maxAcqPull.seconds') as FormControl;
  }

  get tokenUrlControl(): FormControl {
    return this.configForm.get('tokenUrl') as FormControl;
  }

  get audienceControl(): FormControl {
    return this.configForm.get('audience') as FormControl;
  }

  get clientIdControl(): FormControl {
    return this.configForm.get('clientId') as FormControl;
  }

  get userNameControl(): FormControl {
    return this.configForm.get('userName') as FormControl;
  }

  get passwordControl(): FormControl {
    return this.configForm.get('password') as FormControl;
  }


  clearFhirServerBaseUrl(): void {
    this.fhirServerBaseUrlControl.setValue('');
    this.fhirServerBaseUrlControl.updateValueAndValidity();
  }


  comparePlanNames(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  clearAuthType(): void {
    this.authTypeControl.setValue('');
    this.authTypeControl.updateValueAndValidity();
  }

  clearAuthKey(): void {
    this.authKeyControl.setValue('');
    this.authKeyControl.updateValueAndValidity();
  }

  clearTokenUrl(): void {
    this.tokenUrlControl.setValue('');
    this.tokenUrlControl.updateValueAndValidity();
  }

  clearAudience(): void {
    this.audienceControl.setValue('');
    this.audienceControl.updateValueAndValidity();
  }

  clearClientId(): void {
    this.clientIdControl.setValue('');
    this.clientIdControl.updateValueAndValidity();
  }

  clearUserName(): void {
    this.userNameControl.setValue('');
    this.userNameControl.updateValueAndValidity();
  }

  clearPassword(): void {
    this.passwordControl.setValue('');
    this.passwordControl.updateValueAndValidity();
  }

  bothOrNoneHoursValidator(formGroup: FormGroup): ValidationErrors | null {
    const minAcqHours = formGroup.get('minAcqPull.hours')?.value;
    const maxAcqHours = formGroup.get('maxAcqPull.hours')?.value;

    const hasMin = minAcqHours !== null && minAcqHours !== undefined && minAcqHours !== '';
    const hasMax = maxAcqHours !== null && maxAcqHours !== undefined && maxAcqHours !== '';

    if ((hasMin && !hasMax) || (!hasMin && hasMax)) {
      return { bothOrNoneHours: true };
    }

    return null;
  }


  submitConfiguration(): void {
    if (this.configForm.valid) {
      if (this.formMode == FormMode.Create) {
        this.dataAcquisitionService.createFhirQueryConfiguration(this.facilityIdControl.value, {
          facilityId: this.facilityIdControl.value,
          fhirServerBaseUrl: this.fhirServerBaseUrlControl.value,
          maxConcurrentRequests: this.maxConcurrentRequestsControl.value,
          ...(this.getAcqPull("minAcqPull") && { minAcquisitionPullTime: this.getAcqPull("minAcqPull") }),
          ...(this.getAcqPull("maxAcqPull") && { maxAcquisitionPullTime: this.getAcqPull("maxAcqPull") }),
          timeZone: this.item.timeZone,
          authentication: this.authTypeControl.value
            ? {
              authType: this.authTypeControl.value,
              key: this.authKeyControl.value || null,
              tokenUrl: this.tokenUrlControl.value || null,
              audience: this.audienceControl.value || null,
              clientId: this.clientIdControl.value || null,
              userName: this.userNameControl.value || null,
              password: this.passwordControl.value || null
            }
            : null
        } as IDataAcquisitionQueryConfigModel).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: response.id, message: "Query Config Created"});
        });
      } else if (this.formMode == FormMode.Edit) {
        this.dataAcquisitionService.updateFhirQueryConfiguration(
          this.facilityIdControl.value,
          {
            facilityId: this.facilityIdControl.value,
            fhirServerBaseUrl: this.fhirServerBaseUrlControl.value,
            maxConcurrentRequests: this.maxConcurrentRequestsControl.value,
            ...(this.getAcqPull("minAcqPull") && { minAcquisitionPullTime: this.getAcqPull("minAcqPull") }),
            ...(this.getAcqPull("maxAcqPull") && { maxAcquisitionPullTime: this.getAcqPull("maxAcqPull") }),
            timeZone: this.item.timeZone,
            authentication: this.authTypeControl.value
              ? {
                authType: this.authTypeControl.value,
                key: this.authKeyControl.value || null,
                tokenUrl: this.tokenUrlControl.value || null,
                audience: this.audienceControl.value || null,
                clientId: this.clientIdControl.value || null,
                userName: this.userNameControl.value || null,
                password: this.passwordControl.value || null
              }
              : null
          } as IDataAcquisitionQueryConfigModel).subscribe((response: IEntityCreatedResponse) => {
            this.submittedConfiguration.emit({id: this.item.id ?? '', message: "Query Config Updated"});
          }
        );
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
