import { Component, EventEmitter, Input, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ReportService } from 'src/app/services/gateway/report/report.service';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormMode } from '../../../models/FormMode.enum';
import { IReportConfigModel } from '../../../interfaces/report/report-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatDialog } from '@angular/material/dialog';
import { ReportConfigDialogComponent } from '../report-config-dialog/report-config-dialog.component';
import { MatExpansionModule } from '@angular/material/expansion';
import { IEntityDeletedResponse } from '../../../interfaces/entity-deleted-response.interface';

@Component({
  selector: 'app-report-config-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    MatExpansionModule
  ],
  templateUrl: './report-config-form.component.html',
  styleUrls: ['./report-config-form.component.scss']
})
export class ReportConfigFormComponent {

  configForm !: FormGroup;
  @Input() item!: IReportConfigModel;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse | IEntityDeletedResponse>();
  constructor(private snackBar: MatSnackBar, private reportService: ReportService, private dialog: MatDialog) {
    this.configForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      reportType: new FormControl('', Validators.required),
      bundlingType: new FormControl('', Validators.required)
    });
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get reportTypeControl(): FormControl {
    return this.configForm.get('reportType') as FormControl;
  }

  get bundlingTypeControl(): FormControl {
    return this.configForm.get('bundlingType') as FormControl;
  }

  clearReportType(): void {
    this.reportTypeControl.setValue('');
    this.reportTypeControl.updateValueAndValidity();
  }

  clearBundlingType(): void {
    this.bundlingTypeControl.setValue('');
    this.bundlingTypeControl.updateValueAndValidity();
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.reportTypeControl.setValue(this.item.reportType);
      this.reportTypeControl.updateValueAndValidity();

      this.bundlingTypeControl.setValue(this.item.bundlingType);
      this.bundlingTypeControl.updateValueAndValidity();
    }
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {
      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.reportTypeControl.setValue(this.item.reportType);
      this.reportTypeControl.updateValueAndValidity();

      this.bundlingTypeControl.setValue(this.item.bundlingType);
      this.bundlingTypeControl.updateValueAndValidity();
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  submitConfiguration(): void {
    if (this.configForm.valid) {
      if (this.formMode == FormMode.Create) {
        this.reportService.createConfiguration(this.item.facilityId, this.reportTypeControl.value, this.bundlingTypeControl.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
      else if (this.formMode == FormMode.Edit) {
        this.reportService.updateConfiguration(this.item.id, this.item.facilityId, this.reportTypeControl.value, this.bundlingTypeControl.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
    }
    else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  //delete report configurations
  deleteReportConfiguration(): void {
    this.reportService.deleteConfiguration(this.item.id).subscribe(() => {
      let reportDeletedResponse: IEntityDeletedResponse = { id: this.item.id, message: "Report Configuration Deleted" };
      this.submittedConfiguration.emit(reportDeletedResponse);
    });
  }


  editReportDialog(): void {
    this.dialog.open(ReportConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Edit report', formMode: FormMode.Edit, viewOnly: false, reportConfig: this.item }
      }).afterClosed().subscribe(res => {
        console.log('Dialog result: ${res}');
        if (res) {
          this.submittedConfiguration.emit(res);
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

}
