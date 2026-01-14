import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';

import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatSelectModule} from '@angular/material/select';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatTooltipModule} from "@angular/material/tooltip";
import {FormMode} from "../../../models/FormMode.enum";
import {IAccountConfigModel} from "../../../interfaces/account/account-config-model.interface";
import {MatDialog} from "@angular/material/dialog";
import {AccountService} from "../../../services/gateway/account/account.service";
import {RoleModel} from "../../../models/role/role-model.model";
import {IApiResponse} from "../../../interfaces/api-response.interface";


@Component({
  selector: 'app-account-config-form',
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
    MatExpansionModule,
    MatTooltipModule,
    MatSelectModule
],
  templateUrl: './account-config-form.component.html',
  styleUrls: ['./account-config-form.component.scss']
})
export class AccountConfigFormComponent {

  @Input() item!: IAccountConfigModel;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;


  @Input()
  set viewOnly(v: boolean) {
    if (v) this._viewOnly = v;
  }

  get viewOnly() {
    return this._viewOnly;
  }

  @Input() allRoles!: RoleModel[];

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IApiResponse>();

  accountForm!: FormGroup;

  roles: string[] = [];


  constructor(private snackBar: MatSnackBar, private accountService: AccountService, private dialog: MatDialog, private fb: FormBuilder) {
    this.accountForm = this.fb.group({
      firstName: ["", Validators.required],
      lastName: ["", Validators.required],
      email: ["", [Validators.required, Validators.email]],
      selectedRoles: new FormControl([], Validators.required),
    });
  }

  get firstName() {
    return this.accountForm.controls['firstName'];
  }

  get lastName() {
    return this.accountForm.controls['lastName'];
  }

  get email() {
    return this.accountForm.controls['email'];
  }

  get rolesControl(): FormControl {
    return this.accountForm.get('selectedRoles') as FormControl;
  }

  compareRoles(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  ngOnInit(): void {
    this.accountForm.reset();

    if (this.item) {
      //set form values
      this.firstName.setValue(this.item.firstName);
      this.lastName.setValue(this.item.lastName);
      this.email.setValue(this.item.email);
      this.rolesControl.setValue(this.item.roles);

      this.firstName.updateValueAndValidity();
      this.lastName.updateValueAndValidity();
      this.email.updateValueAndValidity();
      this.rolesControl.updateValueAndValidity();
    }

    if (this.allRoles) {
      this.roles = this.allRoles.map((role: RoleModel) => role.name);
    }

    this.accountForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.accountForm.invalid);
    });
  }



  submitConfiguration(): void {
    let user = {
      ...this.item,
      firstName: this.firstName.value,
      lastName: this.lastName.value,
      email: this.email.value,
      roles: this.rolesControl.value,
      username: this.firstName.value + '.' + this.lastName.value,
      isActive: true,
      isDeleted: false
    };
    if (this.accountForm.status == 'VALID') {
      if (this.formMode == FormMode.Create) {
        this.accountService.createUser(user).subscribe({
          next: (response) => {
            if (response) {
              this.submittedConfiguration.emit({success: true, message: ""});
            }
          },
          error: (err) => {
            this.ValidateAccountExists(err);
          }
        });
      } else if (this.formMode == FormMode.Edit) {
          this.accountService.updateUser(user).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({success: true, message: ""});
            },
            error: (err) => {
              this.ValidateAccountExists(err);
            }
          });
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

  private ValidateAccountExists(err: any) {
    if (err.status === 409) {
      console.error('Error occurred:', err); // Log the error or display it to the user
      this.submittedConfiguration.emit({success: false, message: `Another account with same email exists.`});
    } else {
      this.submittedConfiguration.emit({success: false, message: err.message});
    }
  }
}
