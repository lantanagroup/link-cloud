import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatChipEditedEvent, MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { IEmailAddress } from '../notification-dashboard/create-notification-dialog/create-notification-dialog.component';
import { NotificationService } from '../../../services/gateway/notification.service';
import { IFacilityChannel, INotificationConfiguration } from '../../../interfaces/notification/notification-configuration-model.interface';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormMode } from '../../../models/FormMode.enum';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';

@Component({
  selector: 'app-notification-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule
],
  templateUrl: './notification-config-form.component.html',
  styleUrls: ['./notification-config-form.component.scss']
})
export class NotificationConfigFormComponent implements OnInit {

  @Input() item!: INotificationConfiguration;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  formMode!: FormMode;
  notificationConfigForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  constructor(
    private snackBar: MatSnackBar,
    private notificationSerice: NotificationService) {

    //initialize form
    this.notificationConfigForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      recipients: new FormControl([], Validators.required),
      emailChannel: new FormControl(false)
    });

  }

  ngOnInit(): void {
    //rest form whenver the dialog opens
    this.notificationConfigForm.reset();

    if (this.item) {
      this.formMode = FormMode.Edit;

      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.recipientControl.setValue(this.item.emailAddresses);
      this.recipientControl.updateValueAndValidity();

      this.emailChannelControl.setValue(this.item.channels.find(x => x.name == "Email")?.enabled);
      this.emailChannelControl.updateValueAndValidity();

      this.formValueChanged.emit(this.notificationConfigForm.invalid);

    }
    else {
      this.formMode = FormMode.Create;

      this.emailChannelControl.setValue(false);
      this.emailChannelControl.updateValueAndValidity();
    }

    //check if form is view only
    if (this.viewOnly) {
      this.facilityIdControl.disable();
      this.recipientControl.disable();
      this.emailChannelControl.disable();
    }

    this.notificationConfigForm.valueChanges.subscribe(val => {
      if (val) {
        this.formValueChanged.emit(this.notificationConfigForm.invalid);
      }
    });
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  //form control getters
  get facilityIdControl(): FormControl {
    return this.notificationConfigForm.get('facilityId') as FormControl;
  }

  get recipientControl(): FormControl {
    return this.notificationConfigForm.get('recipients') as FormControl;
  }

  get emailChannelControl(): FormControl {
    return this.notificationConfigForm.get('emailChannel') as FormControl;
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

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  submitConfiguration() {
    if (this.formMode == FormMode.Create) {
      let channels: IFacilityChannel[] = [];
      channels.push({ name: 'Email', enabled: this.emailChannelControl.value });
      this.notificationSerice.createFacilityConfiguration(this.facilityIdControl.value, this.recipientControl.value, channels)
        .subscribe(data => {
          this.submittedConfiguration.emit(data);
        });
    }
    else {
      let channels: IFacilityChannel[] = [];
      channels.push({ name: 'Email', enabled: this.emailChannelControl.value });
      this.notificationSerice.updateFacilityConfiguration(this.item.id, this.facilityIdControl.value, this.recipientControl.value, channels)
        .subscribe(data => {
          this.submittedConfiguration.emit(data);
        });
    }
  }

}
