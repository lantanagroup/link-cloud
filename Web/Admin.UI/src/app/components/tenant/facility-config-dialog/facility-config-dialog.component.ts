import { AfterViewInit, Component, OnInit, Inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { FacilityConfigFormComponent } from '../facility-config-form/facility-config-form.component';

@Component({
  selector: 'app-facility-config-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    FacilityConfigFormComponent
  ],
  templateUrl: './facility-config-dialog.component.html',
  styleUrls: ['./facility-config-dialog.component.scss']
})
export class FacilityConfigDialogComponent implements OnInit, AfterViewInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  facilityConfig!: IFacilityConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(FacilityConfigFormComponent) facilityConfigForm!: FacilityConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, viewOnly: boolean, facilityConfig: IFacilityConfigModel },
    private dialogRef: MatDialogRef<FacilityConfigDialogComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.facilityConfig = this.data.facilityConfig;
    this.formMode = this.facilityConfig ? FormMode.Edit : FormMode.Create;
  }

  ngAfterViewInit() {
    //console.log('Values on ngAfterViewInit():');
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
      this.snackBar.open(`Failed to create/update facility, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.facilityConfigForm.submitConfiguration();
  }

}
