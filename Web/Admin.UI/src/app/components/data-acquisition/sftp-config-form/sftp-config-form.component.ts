import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { MatRadioModule } from '@angular/material/radio';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormMode } from 'src/app/models/FormMode.enum';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {
  IAdtConfiguration,
  ICreateSftpConfigurationModel,
  ISftpAcquisitionTypeConfiguration,
  ISftpConfigurationModel,
  ISftpCredentialStatusModel,
  SftpAcquisitionSubType,
  SftpAcquisitionType
} from '../../../interfaces/data-acquisition/sftp-config-model.interface';

type AdtPatientIdSource = 'adtEvent' | 'mrnSearch';

// Keep in step with Hl7Path on the Data Acquisition service, which validates these again when they are saved.
const HL7_LOCATION = /^[A-Za-z][A-Za-z0-9]{2}(\[([1-9]\d{0,2}|[1-9]\d{0,2}(\.[1-9]\d{0,2})?=[^\]]+)\])?[-.][1-9]\d{0,2}(\[([1-9]\d{0,2}|[1-9]\d{0,2}=[^\]]+)\])?(\.[1-9]\d{0,2}(\.[1-9]\d{0,2})?)?$/;

@Component({
  selector: 'app-sftp-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatSelectModule,
    MatExpansionModule,
    MatTooltipModule,
    MatRadioModule
  ],
  templateUrl: './sftp-config-form.component.html',
  styleUrls: ['./sftp-config-form.component.scss']
})
export class SftpConfigFormComponent implements OnInit, OnChanges {
  @Input() item!: ISftpConfigurationModel;

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
  credentialStatus: ISftpCredentialStatusModel | null = null;
  showCredentialFields: boolean = false;
  acquisitionTypes = Object.values(SftpAcquisitionType);
  acquisitionSubTypes = Object.values(SftpAcquisitionSubType);
  readonly adtSubType = SftpAcquisitionSubType.HL7ADT;

  constructor(
    private snackBar: MatSnackBar,
    private dataAcquisitionService: DataAcquisitionService,
    private fb: FormBuilder
  ) {
    this.configForm = this.fb.group({
      facilityId: this.fb.control('', Validators.required),
      host: this.fb.control('', Validators.required),
      port: this.fb.control(22, [Validators.required, Validators.min(1), Validators.max(65535)]),
      remoteDirectory: this.fb.control('/', Validators.required),
      timeout: this.fb.control('00:01:00', Validators.required),
      removeAfterProcessing: this.fb.control(false),
      enableBenchmarking: this.fb.control(false),
      acquisitionConfigurations: this.fb.array([]),
      username: this.fb.control(''),
      password: this.fb.control('')
    }, { validators: SftpConfigFormComponent.adtFilesHandledValidator });
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {
      this.facilityIdControl.setValue(this.item.organizationId);
      this.facilityIdControl.updateValueAndValidity();
      this.hostControl.setValue(this.item.host);
      this.hostControl.updateValueAndValidity();
      this.portControl.setValue(this.item.port);
      this.portControl.updateValueAndValidity();
      this.remoteDirectoryControl.setValue(this.item.remoteDirectory || '/');
      this.remoteDirectoryControl.updateValueAndValidity();
      this.timeoutControl.setValue(this.item.timeout);
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(this.item.removeAfterProcessing);
      this.removeAfterProcessingControl.updateValueAndValidity();
      this.enableBenchmarkingControl.setValue(this.item.enableBenchmarking || false);
      this.enableBenchmarkingControl.updateValueAndValidity();

      // Load existing acquisition configurations
      this.loadAcquisitionConfigurations();

      // Load credential status if editing
      if (this.formMode === FormMode.Edit) {
        this.loadCredentialStatus();
      }
    } else {
      // Set defaults for new configuration
      this.portControl.setValue(22);
      this.portControl.updateValueAndValidity();
      this.timeoutControl.setValue('00:01:00');
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(false);
      this.removeAfterProcessingControl.updateValueAndValidity();
      this.enableBenchmarkingControl.setValue(false);
      this.enableBenchmarkingControl.updateValueAndValidity();
      this.formMode = FormMode.Create;
      this.showCredentialFields = true;
    }

    this.toggleViewOnly(this.viewOnly);

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    // Always reset the form when item changes
    this.configForm.reset();

    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.organizationId);
      this.facilityIdControl.updateValueAndValidity();
      this.hostControl.setValue(this.item.host);
      this.hostControl.updateValueAndValidity();
      this.portControl.setValue(this.item.port);
      this.portControl.updateValueAndValidity();
      this.remoteDirectoryControl.setValue(this.item.remoteDirectory || '/');
      this.remoteDirectoryControl.updateValueAndValidity();
      this.timeoutControl.setValue(this.item.timeout);
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(this.item.removeAfterProcessing);
      this.removeAfterProcessingControl.updateValueAndValidity();
      this.enableBenchmarkingControl.setValue(this.item.enableBenchmarking || false);
      this.enableBenchmarkingControl.updateValueAndValidity();

      // Load existing acquisition configurations
      this.loadAcquisitionConfigurations();

      // Load credential status if editing
      if (this.formMode === FormMode.Edit) {
        this.loadCredentialStatus();
      }
    } else {
      // Set defaults for new configuration
      this.portControl.setValue(22);
      this.portControl.updateValueAndValidity();
      this.timeoutControl.setValue('00:01:00');
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(false);
      this.removeAfterProcessingControl.updateValueAndValidity();
      this.enableBenchmarkingControl.setValue(false);
      this.enableBenchmarkingControl.updateValueAndValidity();
      this.showCredentialFields = true;
    }

    this.toggleViewOnly(this.viewOnly);
  }

  loadCredentialStatus(): void {
    if (this.item?.organizationId) {
      this.dataAcquisitionService.getSftpCredentialStatus(this.item.organizationId).subscribe({
        next: (status) => {
          this.credentialStatus = status;
        },
        error: () => {
          this.credentialStatus = { hasCredentials: false };
        }
      });
    }
  }

  toggleCredentialFields(): void {
    this.showCredentialFields = !this.showCredentialFields;
    if (!this.showCredentialFields) {
      this.usernameControl.setValue('');
      this.passwordControl.setValue('');
    }
  }

  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.hostControl.disable();
      this.portControl.disable();
      this.remoteDirectoryControl.disable();
      this.timeoutControl.disable();
      this.removeAfterProcessingControl.disable();
      this.enableBenchmarkingControl.disable();
      this.usernameControl.disable();
      this.passwordControl.disable();
      this.acquisitionConfigurationsArray.controls.forEach(ctrl => ctrl.disable());
    } else {
      this.hostControl.enable();
      this.portControl.enable();
      this.remoteDirectoryControl.enable();
      this.timeoutControl.enable();
      this.removeAfterProcessingControl.enable();
      this.enableBenchmarkingControl.enable();
      this.usernameControl.enable();
      this.passwordControl.enable();
      this.acquisitionConfigurationsArray.controls.forEach(ctrl => ctrl.enable());
    }
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get hostControl(): FormControl {
    return this.configForm.get('host') as FormControl;
  }

  get portControl(): FormControl {
    return this.configForm.get('port') as FormControl;
  }

  get remoteDirectoryControl(): FormControl {
    return this.configForm.get('remoteDirectory') as FormControl;
  }

  get timeoutControl(): FormControl {
    return this.configForm.get('timeout') as FormControl;
  }

  get removeAfterProcessingControl(): FormControl {
    return this.configForm.get('removeAfterProcessing') as FormControl;
  }

  get usernameControl(): FormControl {
    return this.configForm.get('username') as FormControl;
  }

  get passwordControl(): FormControl {
    return this.configForm.get('password') as FormControl;
  }

  get enableBenchmarkingControl(): FormControl {
    return this.configForm.get('enableBenchmarking') as FormControl;
  }

  get acquisitionConfigurationsArray(): FormArray {
    return this.configForm.get('acquisitionConfigurations') as FormArray;
  }

  loadAcquisitionConfigurations(): void {
    this.acquisitionConfigurationsArray.clear();
    if (this.item?.acquisitionConfigurations) {
      this.item.acquisitionConfigurations.forEach(config => {
        this.acquisitionConfigurationsArray.push(this.createAcquisitionConfigGroup(config));
      });
    }
  }

  createAcquisitionConfigGroup(config?: ISftpAcquisitionTypeConfiguration): FormGroup {
    const adt = config?.adtConfiguration;
    const patientIdSource: AdtPatientIdSource = adt?.mrnIdentifierSystem ? 'mrnSearch' : 'adtEvent';

    const group = this.fb.group({
      acquisitionType: [config?.acquisitionType || SftpAcquisitionType.Census, Validators.required],
      subType: [config?.subType || SftpAcquisitionSubType.None, Validators.required],
      remoteDirectory: [config?.remoteDirectory || '', SftpConfigFormComponent.notBlankValidator],
      processedDirectory: [config?.processedDirectory || '', SftpConfigFormComponent.notBlankValidator],
      fileNamePattern: [config?.fileNamePattern || ''],
      parsingConfiguration: [config?.parsingConfiguration || null],
      adtConfiguration: this.fb.group({
        patientIdSource: [patientIdSource],
        patientIdLocation: [adt?.patientIdLocation || '',
          SftpConfigFormComponent.adtSourceValidator('adtEvent', SftpConfigFormComponent.hl7LocationValidator)],
        mrnIdentifierSystem: [adt?.mrnIdentifierSystem || '',
          SftpConfigFormComponent.adtSourceValidator('mrnSearch', SftpConfigFormComponent.identifierSystemValidator)],
        mrnLocation: [adt?.mrnLocation || '', SftpConfigFormComponent.hl7LocationValidator],
        encounterIdLocation: [adt?.encounterIdLocation || '', SftpConfigFormComponent.hl7LocationValidator]
      })
    });

    // Which ADT fields are required depends on these two controls, so re-check them when either changes
    const revalidateAdtFields = () => {
      group.get('adtConfiguration.patientIdLocation')?.updateValueAndValidity();
      group.get('adtConfiguration.mrnIdentifierSystem')?.updateValueAndValidity();
    };
    group.get('subType')?.valueChanges.subscribe(() => revalidateAdtFields());
    group.get('adtConfiguration.patientIdSource')?.valueChanges.subscribe(() => revalidateAdtFields());
    revalidateAdtFields();

    return group;
  }

  /**
   * Requires and checks a field only when this acquisition is HL7 ADT and identifies patients the way the field is for.
   * Otherwise the field is hidden, and what it holds is not submitted, so it must not block saving.
   */
  private static adtSourceValidator(source: AdtPatientIdSource, format: ValidatorFn): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const adtGroup = control.parent;
      if (adtGroup?.parent?.get('subType')?.value !== SftpAcquisitionSubType.HL7ADT
        || adtGroup.get('patientIdSource')?.value !== source) {
        return null;
      }
      return control.value?.trim() ? format(control) : { required: true };
    };
  }

  /**
   * A system goes into the search as system|value, so it cannot contain that separator, or spaces. Spaces around it are
   * trimmed when it is submitted.
   */
  private static identifierSystemValidator(control: AbstractControl): ValidationErrors | null {
    return /^[^\s|]*$/.test(control.value?.trim() ?? '') ? null : { pattern: true };
  }

  /**
   * Allows empty, otherwise requires a location such as PID-2, PID-3[5=MR].1 or OBX[3.1=FHIRID]-5.
   */
  static hl7LocationValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value?.trim();
    return !value || HL7_LOCATION.test(value) ? null : { hl7Location: true };
  }

  /**
   * ADT events are a stream, so a file read twice is wasted work on every run: it must leave the directory once read.
   */
  static adtFilesHandledValidator(control: AbstractControl): ValidationErrors | null {
    const form = control as FormGroup;
    if (form.get('removeAfterProcessing')?.value) {
      return null;
    }

    const configurations = form.get('acquisitionConfigurations') as FormArray | null;
    const unhandled = configurations?.getRawValue().some((config: ISftpAcquisitionTypeConfiguration) =>
      config.subType === SftpAcquisitionSubType.HL7ADT && !config.processedDirectory?.trim());

    return unhandled ? { adtFilesNotHandled: true } : null;
  }

  /**
   * Keeps only what applies to the chosen way of identifying patients, so the service receives exactly one of the two.
   */
  private toAdtConfiguration(value: any): IAdtConfiguration {
    const mrnSearch = value.patientIdSource === 'mrnSearch';
    return {
      patientIdLocation: mrnSearch ? undefined : value.patientIdLocation?.trim(),
      mrnIdentifierSystem: mrnSearch ? value.mrnIdentifierSystem?.trim() : undefined,
      mrnLocation: value.mrnLocation?.trim() || undefined,
      encounterIdLocation: value.encounterIdLocation?.trim() || undefined
    };
  }

  /**
   * Validator that allows empty (inherits from parent) but rejects whitespace-only values.
   */
  static notBlankValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (typeof value === 'string' && value.length > 0 && value.trim().length === 0) {
      return { blank: true };
    }
    return null;
  }

  addAcquisitionConfiguration(): void {
    this.acquisitionConfigurationsArray.push(this.createAcquisitionConfigGroup());
  }

  removeAcquisitionConfiguration(index: number): void {
    this.acquisitionConfigurationsArray.removeAt(index);
  }

  clearHost(): void {
    this.hostControl.setValue('');
    this.hostControl.updateValueAndValidity();
  }

  clearRemoteDirectory(): void {
    this.remoteDirectoryControl.setValue('/');
    this.remoteDirectoryControl.updateValueAndValidity();
  }

  /**
   * The message of a failed save: a plain-text body, or the detail of an RFC Problem Details body.
   */
  private saveErrorMessage(err: any, fallback: string): string {
    const body = err?.error;

    if (typeof body === 'string' && body.trim().length > 0) {
      return body;
    }

    if (typeof body?.detail === 'string' && body.detail.trim().length > 0) {
      return body.detail;
    }

    return fallback;
  }

  submitConfiguration(): void {
    if (this.configForm.valid) {
      const config: ICreateSftpConfigurationModel = {
        organizationId: this.facilityIdControl.value,
        host: this.hostControl.value,
        port: this.portControl.value,
        remoteDirectory: this.remoteDirectoryControl.value,
        timeout: this.timeoutControl.value,
        removeAfterProcessing: this.removeAfterProcessingControl.value,
        authenticationProtocol: 'Basic',
        enableBenchmarking: this.enableBenchmarkingControl.value,
        acquisitionConfigurations: this.acquisitionConfigurationsArray.value.map((ac: any) => ({
          ...ac,
          remoteDirectory: ac.remoteDirectory || undefined,
          processedDirectory: ac.processedDirectory || undefined,
          adtConfiguration: ac.subType === SftpAcquisitionSubType.HL7ADT ? this.toAdtConfiguration(ac.adtConfiguration) : undefined
        }))
      };

      // Add credentials if provided
      if (this.usernameControl.value && this.passwordControl.value) {
        config.credentials = {
          username: this.usernameControl.value,
          password: this.passwordControl.value
        };
      }

      if (this.formMode === FormMode.Create) {
        this.dataAcquisitionService.createSftpConfiguration(this.facilityIdControl.value, config).subscribe({
          next: (response) => {
            this.submittedConfiguration.emit({ id: response.id, message: 'SFTP Configuration Created' });
          },
          error: (err) => {
            this.snackBar.open(this.saveErrorMessage(err, 'Failed to create SFTP configuration'), '', {
              duration: 3500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        });
      } else if (this.formMode === FormMode.Edit) {
        config.id = this.item.id;
        this.dataAcquisitionService.updateSftpConfiguration(
          this.facilityIdControl.value,
          this.item.id!,
          config
        ).subscribe({
          next: (response) => {
            // If credentials were provided, update them separately
            if (config.credentials) {
              this.dataAcquisitionService.updateSftpCredentials(
                this.facilityIdControl.value,
                config.credentials
              ).subscribe({
                next: () => {
                  this.submittedConfiguration.emit({ id: response.id, message: 'SFTP Configuration Updated' });
                },
                error: () => {
                  this.submittedConfiguration.emit({ id: response.id, message: 'SFTP Configuration Updated (credentials update failed)' });
                }
              });
            } else {
              this.submittedConfiguration.emit({ id: response.id, message: 'SFTP Configuration Updated' });
            }
          },
          error: (err) => {
            this.snackBar.open(this.saveErrorMessage(err, 'Failed to update SFTP configuration'), '', {
              duration: 3500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        });
      }
    } else {
      this.snackBar.open('Invalid form, please check for errors.', '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }
}
