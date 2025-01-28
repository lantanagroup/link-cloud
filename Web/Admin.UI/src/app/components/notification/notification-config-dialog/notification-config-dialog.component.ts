import { AfterViewInit, Component, Inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { INotificationConfiguration } from '../../../interfaces/notification/notification-configuration-model.interface';
import { NotificationConfigFormComponent } from '../notification-config-form/notification-config-form.component';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FormMode } from '../../../models/FormMode.enum';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-notification-config-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    NotificationConfigFormComponent
  ],
  templateUrl: './notification-config-dialog.component.html',
  styleUrls: ['./notification-config-dialog.component.scss']
})
export class NotificationConfigDialogComponent implements OnInit, AfterViewInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  notificationconfig!: INotificationConfiguration;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(NotificationConfigFormComponent) notificationConfigForm!: NotificationConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, viewOnly: boolean, notificationconfig: INotificationConfiguration },
    private dialogRef: MatDialogRef<NotificationConfigDialogComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit() {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.notificationconfig = this.data.notificationconfig;
    this.formMode = this.notificationconfig ? FormMode.Edit : FormMode.Create;
  }

  ngAfterViewInit() {
    console.log('Values on ngAfterViewInit():');
    console.log("Notification Configuration Form:", this.notificationConfigForm);
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IEntityCreatedResponse) {
    if (outcome.id.length > 0) {
      this.dialogRef.close(outcome.message);
    }
    else {
      this.snackBar.open(`Failed to create notification, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.notificationConfigForm.submitConfiguration();
  }

}
