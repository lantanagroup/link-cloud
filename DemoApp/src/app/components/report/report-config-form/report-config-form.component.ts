import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ReportService } from 'src/app/services/gateway/report/report.service';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators} from '@angular/forms';
import { FormMode } from '../../../models/FormMode.enum';
import { IReportConfigModel } from '../../../interfaces/report/report-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
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
import {MatTooltipModule} from "@angular/material/tooltip";
import { ReportTypeValidator } from '../../validators/ReportTypeValidator';
import { map } from 'rxjs';

@Component({
  selector: 'app-report-config-form',
  providers: [
    ReportTypeValidator
  ],
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
    MatExpansionModule,
    MatTooltipModule,
    MatSelectModule
  ],
  templateUrl: './report-config-form.component.html',
  styleUrls: ['./report-config-form.component.scss']
})
export class ReportConfigFormComponent implements OnInit, OnChanges {

  @Input() item!: IReportConfigModel;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse | IEntityDeletedResponse>();

  configForm!: any;

  constructor(private snackBar: MatSnackBar, private reportService: ReportService, private dialog: MatDialog, private fb: FormBuilder, private reportTypeValidator: ReportTypeValidator) {
    this.configForm = this.fb.group({
      facilityId: ["", Validators.required],
      reportType: ["", {
                        validators: [Validators.required],
                        asyncValidators: [this.existsReportType.bind(this)],
                        updateOn: 'blur'
                        }],
      bundlingType: ['Default', Validators.required]
    })
  }

  existsReportType(control: AbstractControl) {
    return this.reportTypeValidator.checkIfReportTypeExists(control.value, this.item).pipe(map((response: boolean) => response ? { reportTypeExists: true } : null));
  }

  get bundlingTypes() {
    return ['Default', 'SharedPatientLineLevel'];
  }
  get reportTypeExists() {
    return this.configForm.get('reportType').hasError('reportTypeExists') && this.configForm.get('reportType').touched;
  }

  get facilityId() {
    return this.configForm.controls['facilityId'];
  }

  get reportType(){
    return this.configForm.controls['reportType'];
  }

  get bundlingType() {
    return this.configForm.controls['bundlingType'];
  }

  clearReportType(): void {
    this.reportType.setValue('');
    this.reportType.updateValueAndValidity();
  }

  clearBundlingType(): void {
    this.bundlingType.setValue('');
    this.bundlingType.updateValueAndValidity();
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.reportType.setValue(this.item.reportType);
      this.reportType.updateValueAndValidity();

      this.bundlingType.setValue(this.item.bundlingType);
      this.bundlingType.updateValueAndValidity();
    }
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {
      //set form values
      this.facilityId.setValue(this.item.facilityId);
      this.facilityId.updateValueAndValidity();

      this.reportType.setValue(this.item.reportType);
      this.reportType.updateValueAndValidity();

      this.bundlingType.setValue(this.item.bundlingType);
      this.bundlingType.updateValueAndValidity();
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  submitConfiguration(): void {
    if (this.configForm.status == 'VALID') {
      if (this.formMode == FormMode.Create) {
        this.reportService.createReportConfiguration(this.item.facilityId, this.reportType.value, this.bundlingType.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
      else if (this.formMode == FormMode.Edit) {
        this.reportService.updateReportConfiguration(this.item.id, this.item.facilityId, this.reportType.value, this.bundlingType.value).subscribe((response: IEntityCreatedResponse) => {
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
    this.reportService.deleteReportConfiguration(this.item.id).subscribe(() => {
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
       // console.log('Dialog result: ${res}');
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
