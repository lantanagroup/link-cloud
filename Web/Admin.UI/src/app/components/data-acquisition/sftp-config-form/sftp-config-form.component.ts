import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSelectModule } from '@angular/material/select';
import { FormMode } from 'src/app/models/FormMode.enum';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {
  ICreateSftpConfigurationModel,
  ISftpConfigurationModel,
  ISftpCredentialStatusModel
} from '../../../interfaces/data-acquisition/sftp-config-model.interface';

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
    MatSelectModule
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

  constructor(
    private snackBar: MatSnackBar,
    private dataAcquisitionService: DataAcquisitionService,
    private fb: FormBuilder
  ) {
    this.configForm = this.fb.group({
      facilityId: this.fb.control('', Validators.required),
      host: this.fb.control('', Validators.required),
      port: this.fb.control(22, [Validators.required, Validators.min(1), Validators.max(65535)]),
      remoteDirectory: this.fb.control(''),
      timeout: this.fb.control('00:01:00', Validators.required),
      removeAfterProcessing: this.fb.control(false),
      username: this.fb.control(''),
      password: this.fb.control('')
    });
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
      this.remoteDirectoryControl.setValue(this.item.remoteDirectory || '');
      this.remoteDirectoryControl.updateValueAndValidity();
      this.timeoutControl.setValue(this.item.timeout);
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(this.item.removeAfterProcessing);
      this.removeAfterProcessingControl.updateValueAndValidity();

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
      this.remoteDirectoryControl.setValue(this.item.remoteDirectory || '');
      this.remoteDirectoryControl.updateValueAndValidity();
      this.timeoutControl.setValue(this.item.timeout);
      this.timeoutControl.updateValueAndValidity();
      this.removeAfterProcessingControl.setValue(this.item.removeAfterProcessing);
      this.removeAfterProcessingControl.updateValueAndValidity();

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
      this.usernameControl.disable();
      this.passwordControl.disable();
    } else {
      this.hostControl.enable();
      this.portControl.enable();
      this.remoteDirectoryControl.enable();
      this.timeoutControl.enable();
      this.removeAfterProcessingControl.enable();
      this.usernameControl.enable();
      this.passwordControl.enable();
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

  clearHost(): void {
    this.hostControl.setValue('');
    this.hostControl.updateValueAndValidity();
  }

  clearRemoteDirectory(): void {
    this.remoteDirectoryControl.setValue('');
    this.remoteDirectoryControl.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if (this.configForm.valid) {
      const config: ICreateSftpConfigurationModel = {
        organizationId: this.facilityIdControl.value,
        host: this.hostControl.value,
        port: this.portControl.value,
        remoteDirectory: this.remoteDirectoryControl.value || undefined,
        timeout: this.timeoutControl.value,
        removeAfterProcessing: this.removeAfterProcessingControl.value,
        authenticationProtocol: 'Basic'
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
            this.snackBar.open(err.error || 'Failed to create SFTP configuration', '', {
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
            this.snackBar.open(err.error || 'Failed to update SFTP configuration', '', {
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
