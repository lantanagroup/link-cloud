import {Component, Inject, OnInit, ViewChild} from '@angular/core';

import {MatButtonModule} from '@angular/material/button';
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from '@angular/material/dialog';
import {MatIconModule} from '@angular/material/icon';
import {FormMode} from 'src/app/models/FormMode.enum';
import {MatSnackBar} from '@angular/material/snack-bar';
import {VendorConfigFormComponent} from "../vendor-config-form/vendor-config-form.component";
import {IApiResponse} from "../../../interfaces/api-response.interface";
import {IVendorConfigModel} from "../../../interfaces/vendor/vendor-config-model.interface";

@Component({
  selector: 'app-vendor-config-dialog',
  standalone: true,
  templateUrl: './vendor-config-dialog.component.html',
  styleUrls: ['./vendor-config-dialog.component.scss'],
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    VendorConfigFormComponent
]
})
export class VendorConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  vendorConfig!: IVendorConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;
  canSave = false;

  @ViewChild(VendorConfigFormComponent) vendorConfigForm!: VendorConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      formMode: FormMode,
      viewOnly: boolean,
      vendorConfig: IVendorConfigModel,
    },
    private dialogRef: MatDialogRef<VendorConfigDialogComponent>,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.vendorConfig = this.data.vendorConfig;
    this.formMode = this.data.formMode;
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
    this.canSave = this.updateCanSave();
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
    if (!this.vendorConfigForm) {
      this.snackBar.open('Form not initialized', '', {
        duration: 3500,
        panelClass: 'error-snackbar'
      });
      return;
    }
    this.vendorConfigForm.submitConfiguration();
  }


  updateCanSave() {
    return (this.vendorConfigForm?.vendorForm.status == 'VALID');
  }

}

