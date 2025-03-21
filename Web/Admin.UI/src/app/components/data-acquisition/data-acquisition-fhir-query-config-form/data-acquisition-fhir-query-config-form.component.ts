import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatToolbarModule} from '@angular/material/toolbar';
import {IDataAcquisitionQueryConfigModel} from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface';
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
    CommonModule,
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
    MatCheckboxModule
  ],
  templateUrl: './data-acquisition-fhir-query-config-form.component.html',
  styleUrls: ['./data-acquisition-fhir-query-config-form.component.scss']
})
export class DataAcquisitionFhirQueryConfigFormComponent {
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

  planNames: string[] = [];
  authTypes: string[] = ["Basic", "Epic", "None"];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.configForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      fhirServerBaseUrl: new FormControl('', Validators.required),
      queryPlanIds: new FormControl([], Validators.required),
      isAuthEnabled: new FormControl(false, Validators.required),
      authType: new FormControl(''),
      authKey: new FormControl(''),
      tokenUrl: new FormControl(''),
      audience: new FormControl(''),
      clientId: new FormControl(''),
      userName: new FormControl(''),
      password: new FormControl('')
    });
  }


  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {

      this.dataAcquisitionService.getQueryPlanNames(this.item.facilityId).subscribe(
        {
          next: (response) => {
            this.planNames = response;
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: '', message: err.message});
          }
        });

      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirServerBaseUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.queryPlanIdsControl.setValue(this.item.queryPlanIds);
      this.queryPlanIdsControl.updateValueAndValidity();

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
    this.updateValidators(this.authTypeControl?.value);

    this.configForm.valueChanges.subscribe(() => {
      if (this.isAuthEnabledControl.value == false) {
        this.configForm.controls['authType'].clearValidators();
        this.configForm.controls['authKey'].clearValidators();
        this.configForm.controls['tokenUrl'].clearValidators();
        this.configForm.controls['audience'].clearValidators();
        this.configForm.controls['clientId'].clearValidators();
        this.configForm.controls['userName'].clearValidators();
        this.configForm.controls['password'].clearValidators();
      } else {
        this.configForm.controls['authType'].setValidators(Validators.required);
      }
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }


  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {

      this.dataAcquisitionService.getQueryPlanNames(this.item.facilityId).subscribe(
        {
          next: (response) => {
            this.planNames = response;
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: '', message: err.message});
          }
        });

      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirServerBaseUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.queryPlanIdsControl.setValue(this.item.queryPlanIds);
      this.queryPlanIdsControl.updateValueAndValidity();

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
    if (viewOnly) {
      this.fhirServerBaseUrlControl.disable();
      this.facilityIdControl.disable();
      this.queryPlanIdsControl.disable();
      this.authTypeControl.disable();
      this.authKeyControl.disable();
      this.tokenUrlControl.disable();
      this.audienceControl.disable();
      this.clientIdControl.disable();
      this.userNameControl.disable();
      this.passwordControl.disable();
      this.isAuthEnabledControl.disable();
    } else {
      this.fhirServerBaseUrlControl.enable();
      this.facilityIdControl.enable();
      this.queryPlanIdsControl.enable();
      this.authTypeControl.enable();
      this.authKeyControl.enable();
      this.tokenUrlControl.enable();
      this.audienceControl.enable();
      this.clientIdControl.enable();
      this.userNameControl.enable();
      this.passwordControl.enable();
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

  get queryPlanIdsControl(): FormControl {
    return this.configForm.get('queryPlanIds') as FormControl;
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

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  clearFhirServerBaseUrl(): void {
    this.fhirServerBaseUrlControl.setValue('');
    this.fhirServerBaseUrlControl.updateValueAndValidity();
  }

  clearQueryPlanIds(): void {
    this.queryPlanIdsControl.setValue('');
    this.queryPlanIdsControl.updateValueAndValidity();
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

  submitConfiguration(): void {
    if (this.configForm.valid) {
      if (this.formMode == FormMode.Create) {
        this.dataAcquisitionService.createFhirQueryConfiguration(this.facilityIdControl.value, {
          facilityId: this.facilityIdControl.value,
          fhirServerBaseUrl: this.fhirServerBaseUrlControl.value,
          authentication:
            {
              "authType": this.authTypeControl.value,
              "key": this.authKeyControl.value,
              "tokenUrl": this.tokenUrlControl.value,
              "audience": this.audienceControl.value,
              "clientId": this.clientIdControl.value,
              "userName": this.userNameControl.value,
              "password": this.passwordControl.value
            },
          queryPlanIds: this.queryPlanIdsControl.value || []
        } as IDataAcquisitionQueryConfigModel).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: response.id, message: "Query Config Created"});
        });
      } else if (this.formMode == FormMode.Edit) {
        this.dataAcquisitionService.updateFhirQueryConfiguration(
          this.facilityIdControl.value,
          {
            facilityId: this.facilityIdControl.value,
            fhirServerBaseUrl: this.fhirServerBaseUrlControl.value,
            authentication:
              {
                "authType": this.authTypeControl.value,
                "key": this.authKeyControl.value,
                "tokenUrl": this.tokenUrlControl.value,
                "audience": this.audienceControl.value,
                "clientId": this.clientIdControl.value,
                "userName": this.userNameControl.value,
                "password": this.passwordControl.value
              },
            queryPlanIds: this.queryPlanIdsControl.value || []
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
