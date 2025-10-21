import { Component, Inject, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ISftpConfigurationModel } from '../../../interfaces/data-acquisition/sftp-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { FormMode } from '../../../models/FormMode.enum';
import { SftpConfigFormComponent } from '../sftp-config-form/sftp-config-form.component';

@Component({
  selector: 'app-sftp-config-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    SftpConfigFormComponent
  ],
  templateUrl: './sftp-config-dialog.component.html',
  styleUrls: ['./sftp-config-dialog.component.scss']
})
export class SftpConfigDialogComponent {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: ISftpConfigurationModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(SftpConfigFormComponent) configForm!: SftpConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      formMode: FormMode,
      viewOnly: boolean,
      sftpConfig: ISftpConfigurationModel
    },
    private dialogRef: MatDialogRef<SftpConfigDialogComponent>,
    private snackBar: MatSnackBar
  ) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.sftpConfig;
    this.formMode = this.data.formMode;
  }

  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IEntityCreatedResponse) {
    if (outcome.message.length > 0) {
      this.dialogRef.close(outcome.message);
    } else {
      this.snackBar.open('Failed to save SFTP configuration, see error for details.', '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.configForm.submitConfiguration();
  }
}
