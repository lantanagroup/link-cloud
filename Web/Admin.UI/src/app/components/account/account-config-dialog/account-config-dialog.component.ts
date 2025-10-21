import {Component, Inject, OnInit, ViewChild} from '@angular/core';

import {MatButtonModule} from '@angular/material/button';
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from '@angular/material/dialog';
import {MatIconModule} from '@angular/material/icon';
import {FormMode} from 'src/app/models/FormMode.enum';
import {MatSnackBar} from '@angular/material/snack-bar';
import {AccountConfigFormComponent} from "../account-config-form/account-config-form.component";
import {IAccountConfigModel} from "../../../interfaces/account/account-config-model.interface";
import {RoleModel} from "../../../models/role/role-model.model";
import {IApiResponse} from "../../../interfaces/api-response.interface";

@Component({
  selector: 'app-account-config-dialog',
  standalone: true,
  templateUrl: './account-config-dialog.component.html',
  styleUrls: ['./account-config-dialog.component.scss'],
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    AccountConfigFormComponent
]
})
export class AccountConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  accountConfig!: IAccountConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;
  roles: string[] = [];
  allRoles: RoleModel[] = [];

  @ViewChild(AccountConfigFormComponent) accountConfigForm!: AccountConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, accountConfig: IAccountConfigModel, allRoles: RoleModel[] },
    private dialogRef: MatDialogRef<AccountConfigDialogComponent>,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.accountConfig = this.data.accountConfig;
    this.allRoles = this.data.allRoles;
    this.formMode = this.data.formMode;
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IApiResponse) {
    if (outcome.success) {
      this.dialogRef.close(outcome);
    } else {
      this.snackBar.open(outcome.message, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    if (!this.accountConfigForm) {
       this.snackBar.open('Form not initialized', '', {
         duration: 3500,
         panelClass: 'error-snackbar'
       });
       return;
    }
    this.accountConfigForm.submitConfiguration();
  }

  canSave() {
    return (this.accountConfigForm?.accountForm.status == 'VALID') ? true : false;
  }

}

