import { COMMA, ENTER } from '@angular/cdk/keycodes';

import { Component, EventEmitter, Input, Output, SimpleChanges } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { IDataAcquisitionAuthenticationConfigModel } from '../../../interfaces/data-acquisition/data-acquisition-auth-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { FormMode } from '../../../models/FormMode.enum';
import { DataAcquisitionService } from '../../../services/gateway/data-acquisition/data-acquisition.service';

@Component({
  selector: 'app-data-acquisition-authentication-config-form',
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
    MatSelectModule
],
  templateUrl: './data-acquisition-authentication-config-form.component.html',
  styleUrls: ['./data-acquisition-authentication-config-form.component.css']
})
export class DataAcquisitionAuthenticationConfigFormComponent {
  @Input() queryAuthConfig!: IDataAcquisitionAuthenticationConfigModel;
  @Input() queryListAuthConfig!: IDataAcquisitionAuthenticationConfigModel;
  @Input() facilityId!: string;

  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  configForm!: FormGroup;
  configTypeStr!: string;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.configForm = new FormGroup({
      configType: new FormControl('', Validators.required),
      facilityId: new FormControl('', Validators.required),
      authType: new FormControl('', Validators.required),
      authKey: new FormControl('', Validators.required),
      tokenUrl: new FormControl('', Validators.required),
      audience: new FormControl('', Validators.required),
      clientId: new FormControl('', Validators.required),
      userName: new FormControl('', Validators.required),
      password: new FormControl('', Validators.required),
    });
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.facilityId) {

      this.facilityIdControl.setValue(this.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      if (this.queryAuthConfig) {
        //set form values
        this.configTypeControl.setValue("fhirQueryConfiguration");
        this.configTypeControl.updateValueAndValidity();

        this.authTypeControl.setValue(this.queryAuthConfig.authType);
        this.authTypeControl.updateValueAndValidity();

        this.authKeyControl.setValue(this.queryAuthConfig.key);
        this.authKeyControl.updateValueAndValidity();

        this.tokenUrlControl.setValue(this.queryAuthConfig.tokenUrl);
        this.tokenUrlControl.updateValueAndValidity();

        this.audienceControl.setValue(this.queryAuthConfig.audience);
        this.audienceControl.updateValueAndValidity();

        this.clientIdControl.setValue(this.queryAuthConfig.clientId);
        this.clientIdControl.updateValueAndValidity();

        this.userNameControl.setValue(this.queryAuthConfig.userName);
        this.userNameControl.updateValueAndValidity();

        this.passwordControl.setValue(this.queryAuthConfig.password);
        this.passwordControl.updateValueAndValidity();
      }
      else if (this.queryListAuthConfig) {

        //set form values
        this.configTypeControl.setValue("fhirQueryListConfiguration");
        this.configTypeControl.updateValueAndValidity();

        this.authTypeControl.setValue(this.queryListAuthConfig.authType);
        this.authTypeControl.updateValueAndValidity();

        this.authKeyControl.setValue(this.queryListAuthConfig.key);
        this.authKeyControl.updateValueAndValidity();

        this.tokenUrlControl.setValue(this.queryListAuthConfig.tokenUrl);
        this.tokenUrlControl.updateValueAndValidity();

        this.audienceControl.setValue(this.queryListAuthConfig.audience);
        this.audienceControl.updateValueAndValidity();

        this.clientIdControl.setValue(this.queryListAuthConfig.clientId);
        this.clientIdControl.updateValueAndValidity();

        this.userNameControl.setValue(this.queryListAuthConfig.userName);
        this.userNameControl.updateValueAndValidity();

        this.passwordControl.setValue(this.queryListAuthConfig.password);
        this.passwordControl.updateValueAndValidity();
      }
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      if (this.queryAuthConfig) {
        //set form values
        this.configTypeControl.setValue("fhirQueryConfiguration");
        this.configTypeControl.updateValueAndValidity();

        this.authTypeControl.setValue(this.queryAuthConfig.authType);
        this.authTypeControl.updateValueAndValidity();

        this.authKeyControl.setValue(this.queryAuthConfig.key);
        this.authKeyControl.updateValueAndValidity();

        this.tokenUrlControl.setValue(this.queryAuthConfig.tokenUrl);
        this.tokenUrlControl.updateValueAndValidity();

        this.audienceControl.setValue(this.queryAuthConfig.audience);
        this.audienceControl.updateValueAndValidity();

        this.clientIdControl.setValue(this.queryAuthConfig.clientId);
        this.clientIdControl.updateValueAndValidity();

        this.userNameControl.setValue(this.queryAuthConfig.userName);
        this.userNameControl.updateValueAndValidity();

        this.passwordControl.setValue(this.queryAuthConfig.password);
        this.passwordControl.updateValueAndValidity();
      }
      else if (this.queryListAuthConfig) {

        //set form values
        this.configTypeControl.setValue("fhirQueryListConfiguration");
        this.configTypeControl.updateValueAndValidity();

        this.authTypeControl.setValue(this.queryListAuthConfig.authType);
        this.authTypeControl.updateValueAndValidity();

        this.authKeyControl.setValue(this.queryListAuthConfig.key);
        this.authKeyControl.updateValueAndValidity();

        this.tokenUrlControl.setValue(this.queryListAuthConfig.tokenUrl);
        this.tokenUrlControl.updateValueAndValidity();

        this.audienceControl.setValue(this.queryListAuthConfig.audience);
        this.audienceControl.updateValueAndValidity();

        this.clientIdControl.setValue(this.queryListAuthConfig.clientId);
        this.clientIdControl.updateValueAndValidity();

        this.userNameControl.setValue(this.queryListAuthConfig.userName);
        this.userNameControl.updateValueAndValidity();

        this.passwordControl.setValue(this.queryListAuthConfig.password);
        this.passwordControl.updateValueAndValidity();
      }
    }
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get authTypeControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get authKeyControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get tokenUrlControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get audienceControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get clientIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get userNameControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get passwordControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get configTypeControl(): FormControl {
    return this.configForm.get('configType') as FormControl;
  }

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
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
        this.dataAcquisitionService.createAuthenticationConfig(this.facilityIdControl.value, this.configTypeControl.value, {
          authType: this.authTypeControl.value,
          key: this.authKeyControl.value,
          tokenUrl: this.tokenUrlControl.value,
          audience: this.audienceControl.value,
          clientId: this.clientIdControl.value,
          userName: this.userNameControl.value,
          password: this.passwordControl.value
        }).subscribe((response: IEntityCreatedResponse) => {
          this.snackBar.open(`FHIR query configuration created.`, 'Close', { duration: 3000 });
          this.submittedConfiguration.emit(response);
        });
      }
      else if (this.formMode == FormMode.Edit) { }

      if (this.configTypeControl.value === "fhirQueryConfiguration") {
        this.dataAcquisitionService.updateAuthenticationConfig(this.facilityIdControl.value, this.configTypeControl.value, {
          authType: this.authTypeControl.value,
          key: this.authKeyControl.value,
          tokenUrl: this.tokenUrlControl.value,
          audience: this.audienceControl.value,
          clientId: this.clientIdControl.value,
          userName: this.userNameControl.value,
          password: this.passwordControl.value
        }).subscribe((response: IEntityCreatedResponse) => {
          this.snackBar.open(`FHIR query configuration created.`, 'Close', { duration: 3000 });
          this.submittedConfiguration.emit(response);
        });
      }
      else if (this.configTypeControl.value === "fhirQueryListConfiguration") {
        this.dataAcquisitionService.createFhirListConfiguration(this.facilityIdControl.value, this.configForm.value).subscribe((response: IEntityCreatedResponse) => {
          this.snackBar.open(`FHIR list configuration created.`, 'Close', { duration: 3000 });
          this.submittedConfiguration.emit(response);
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
