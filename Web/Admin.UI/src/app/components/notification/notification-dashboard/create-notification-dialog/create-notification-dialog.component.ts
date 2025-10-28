import { Component, OnInit } from '@angular/core';
import { MatChipEditedEvent, MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { COMMA, ENTER } from '@angular/cdk/keycodes';

import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NotificationService } from '../../../../services/gateway/notification.service';

export interface IEmailAddress {
  name: string;
}

@Component({
  selector: 'app-create-notification-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    ReactiveFormsModule,
    MatSnackBarModule
],
  templateUrl: './create-notification-dialog.component.html',
  styleUrls: ['./create-notification-dialog.component.scss']
})
export class CreateNotificationDialogComponent implements OnInit {
  createNotificationForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  facilityOptions = ["FACILITY_ORG_10001", "FACILITY_ORG_12345"]; //temp until tenant functionality is brought into the ui
  notificationTypeOptions = ["System Notification", "Test Notification"]; //temp until notification types are added to notification service

  constructor(
    private dialogRef: MatDialogRef<CreateNotificationDialogComponent>,
    private snackBar: MatSnackBar,
    private notificationSerice: NotificationService) {

    //initialize form
    this.createNotificationForm = new FormGroup({
      notificationType: new FormControl('', Validators.required),
      facilityId: new FormControl(''),
      recipients: new FormControl([], Validators.required),
      bcc: new FormControl([]),
      subject: new FormControl('', Validators.required),
      body: new FormControl('', Validators.required)
    });

  }

  ngOnInit(): void {
    //rest form whenver the dialog opens
    this.createNotificationForm.reset();
  }

  //form control getters
  get notificationTypeControl(): FormControl {
    return this.createNotificationForm.get('notificationType') as FormControl;
  }

  get facilityControl(): FormControl {
    return this.createNotificationForm.get('facilityId') as FormControl;
  }

  get recipientControl(): FormControl {
    return this.createNotificationForm.get('recipients') as FormControl;
  }

  get bccControl(): FormControl {
    return this.createNotificationForm.get('bcc') as FormControl;
  }

  get subjectControl(): FormControl {
    return this.createNotificationForm.get('subject') as FormControl;
  }

  get messageControl(): FormControl {
    return this.createNotificationForm.get('body') as FormControl;
  }


  //recipient control methods
  addRecipient(event: MatChipInputEvent): void {
    const value = (event.value || '').trim();

    // Add recipient
    if (value && this.validateEmailAddress(value)) {
      if (this.recipientControl.value == null) {
        this.recipientControl.setValue([]);
      }
      this.recipientControl.value.push(value);
      this.recipientControl.updateValueAndValidity();
    }

    // Clear the input value
    event.chipInput!.clear();
  }

  removeRecipient(email: IEmailAddress): void {
    const index = this.recipientControl.value.indexOf(email);

    if (index >= 0) {
      this.recipientControl.value.splice(index, 1);
      this.recipientControl.updateValueAndValidity();
    }
  }

  editRecipient(email: IEmailAddress, event: MatChipEditedEvent) {
    const value = event.value.trim();

    // Remove recipient if it no longer has a name
    if (!value || !this.validateEmailAddress(value)) {
      this.removeRecipient(email);
      return;
    }

    // Edit existing recipient
    const index = this.recipientControl.value.indexOf(email);
    if (index >= 0) {
      this.recipientControl.value[index] = value;
      this.recipientControl.updateValueAndValidity();
    }
  }

  //bcc control methods
  addBcc(event: MatChipInputEvent): void {
    const value = (event.value || '').trim();

    // Add recipient
    if (value && this.validateEmailAddress(value)) {
      if (this.bccControl.value == null) {
        this.bccControl.setValue([]);
      }
      this.bccControl.value.push(value);
      this.bccControl.updateValueAndValidity();
    }

    // Clear the input value
    event.chipInput!.clear();
  }

  removeBcc(email: IEmailAddress): void {
    const index = this.bccControl.value.indexOf(email);

    if (index >= 0) {
      this.bccControl.value.splice(index, 1);
      this.bccControl.updateValueAndValidity();
    }
  }

  editBcc(email: IEmailAddress, event: MatChipEditedEvent) {
    const value = event.value.trim();

    // Remove recipient if it no longer has a name
    if (!value || !this.validateEmailAddress(value)) {
      this.removeBcc(email);
      return;
    }

    // Edit existing recipient
    const index = this.bccControl.value.indexOf(email);
    if (index >= 0) {
      this.bccControl.value[index] = value;
      this.bccControl.updateValueAndValidity();
    }
  }

  validateEmailAddress(email: string) {
    var mailformat = /^\w+([\.-]?\w+)*@\w+([\.-]?\w+)*(\.\w{2,3})+$/;
    if (email.match(mailformat)) {
      return true;
    }
    else {
      this.snackBar.open(`'${email}' is not a valid email address.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      return false;
    }
  }

  clearSubjet(): void {
    this.subjectControl.setValue('');
    this.subjectControl.updateValueAndValidity();
  }

  sendNotification(): void {
    if (this.createNotificationForm.valid) {

      this.notificationSerice.create(this.notificationTypeControl.value, this.facilityControl.value, this.subjectControl.value,
        this.messageControl.value, this.recipientControl.value, this.bccControl.value).subscribe(data => {
          if (data.id) {
            this.dialogRef.close(data.message);
          }
          else {
            this.snackBar.open(`Failed to create notification, see error for details.`, '', {
              duration: 3500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        });



    }
  }
}
