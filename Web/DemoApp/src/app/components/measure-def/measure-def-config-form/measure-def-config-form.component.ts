import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from '../../../interfaces/entity-deleted-response.interface';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { FormMode } from '../../../models/FormMode.enum';
import { MeasureDefinitionService } from '../../../services/gateway/measure-definition/measure.service';
import { FileUploadComponent } from '../../file-upload/file-upload.component';
import { BundleIdValidator } from '../../validators/BundleIdValidator';
import { UrlOrBundleValidator } from '../../validators/UrlOrBundleValidator';
import {MatButtonModule} from "@angular/material/button";

@Component({
  selector: 'app-measure-def-config-form',
  standalone: true,
  providers: [
    UrlOrBundleValidator,
    BundleIdValidator
  ],
  imports: [
    CommonModule,
    MatIconModule,
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
    MatSelectModule,
    FileUploadComponent,
    MatProgressSpinnerModule,
    MatButtonModule
  ],
  templateUrl: './measure-def-config-form.component.html',
  styleUrls: ['./measure-def-config-form.component.scss']
})
export class MeasureDefinitionFormComponent implements OnInit, OnChanges {

  configForm!: any;

  @Input() item!: IMeasureDefinitionConfigModel;

  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;

  private _fileName = "";

  public _processing = false;

  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse | IEntityDeletedResponse>();

  constructor(private formBuilder: FormBuilder, private measureDefinitionService: MeasureDefinitionService, private urlOrBundleValidator: UrlOrBundleValidator, private bundleIdValidator: BundleIdValidator, private snackBar: MatSnackBar) {

    const reg: string = '(https?://)?([\\da-z.-]+)\\.([a-z.]{2,6})[/\\w .-]*/?';


    this.configForm = this.formBuilder.group(
      {
        bundleId: [ "", Validators.required],
        bundleName: ["", Validators.required],
        url: [null, Validators.pattern(reg)],
        bundle: [null],
      },
      {
        validators: [this.validateBundleUrlAndFile.bind(this), this.validateBundleId.bind(this)]
      }
    );
  }


  validateBundleUrlAndFile(control: FormGroup) {
    //return this.configForm?.controls['url']?.value || this.configForm?.controls['bundle']?.value ? null : { bundleAndUrlMissing: true };
     return this.urlOrBundleValidator.bundleAndUrlMissing(control.value) ? { bundleAndUrlMissing: true } : null;
  }

  validateBundleId(control: FormGroup) {
    return this.bundleIdValidator.invalidBundleId(control.value, this.item) ? { invalidBundleId: true } : null;
  }

  get invalidBundleId() {
    return this.configForm.hasError('invalidBundleId') && this.configForm.touched;
  }


  get bundleAndUrlMissing() {
    return this.configForm.hasError('bundleAndUrlMissing') && this.configForm.touched ;
  }


  get bundleId() {
    return this.configForm.controls['bundleId'];
  }

  get bundleName() {
    return this.configForm.controls['bundleName'];
  }

  get url() {
    return this.configForm.controls['url'];
  }

  get bundle() {
    return this.configForm.controls['bundle'];
  }
  get fileName() {
    return this._fileName;
  }

  set fileName(value: string) {
    this._fileName = value;
  }

  loadFile(file: any) {
    this.bundle.setValue(file);
    if (!file) {
      this.fileName= 'No bundle';
    }
    else if (file?.["id"]) {
      this.bundleId.setValue(file["id"]);
    }
  }

  get isEditMode() {
    return this.formMode == FormMode.Edit;
  }

  get processing() {
    return this._processing;
  }

  set processing(value) {
    this._processing = value;
  }


  ngOnChanges(changes: SimpleChanges) {
    if (changes['item'] && changes['item'].currentValue) {
      this.bundleId.setValue(this.item.bundleId);
      this.bundleId.updateValueAndValidity();

      this.bundleName.setValue(this.item.bundleName);
      this.bundleName.updateValueAndValidity();

      this.url.setValue(this.item.url);
      this.url.updateValueAndValidity

      this.bundle.setValue(this.item.bundle);
      this.bundle.updateValueAndValidity();

    }
    if (this.formMode == FormMode.Edit) {
       this.configForm.get('bundleId')?.disable();
    }
  }

  ngOnInit(): void {

    this.configForm.reset();

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });


    if (this.item) {
      //set form values
      this.bundleId.setValue(this.item.bundleId);
      this.bundleId.updateValueAndValidity();

      this.bundleName.setValue(this.item.bundleName);
      this.bundleName.updateValueAndValidity();

      this.url.setValue(this.item.url);
      this.url.updateValueAndValidity();

      this.bundle.setValue(this.item.bundle);
      this.bundle.updateValueAndValidity();

      this.fileName = this.formMode == FormMode.Create ? 'No bundle' : this.item.bundleId;
    }

  }


  clearBundleId(): void {
    this.bundleId.setValue('');
    this.bundleId.updateValueAndValidity();
  }

  clearBundleName(): void {
    this.bundleName.setValue('');
    this.bundleName.updateValueAndValidity();
  }

  clearBundleUrl(): void {
    this.url.setValue(null);
    this.url.updateValueAndValidity();
  }

  clearBundle(): void {
    this.bundle.setValue(null);
    this.bundle.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if (this.configForm.status == 'VALID') {
      this.processing = true;
      if (this.formMode == FormMode.Create) {
        var createMeasureConfig: IMeasureDefinitionConfigModel = { 'bundleId': this.bundleId.value, 'bundleName': this.bundleName.value, 'bundle': this.bundle.value, 'url': this.url.value };
        this.measureDefinitionService.createMeasureDefinitionConfiguration(createMeasureConfig).subscribe((response: IEntityCreatedResponse) => {
          this.processing = false;
          this.submittedConfiguration.emit(response);
        },
        error => {
          this.snackBar.open(`Please check for errors: ${error.statusText}`, '', {
              duration: 5000,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
        });
      }
      else if (this.formMode == FormMode.Edit) {
        var updateMeasureConfig: IMeasureDefinitionConfigModel = { 'bundleId': this.bundleId.value, 'bundleName': this.bundleName.value,  'bundle': this.bundle.value, 'url': this.url.value };
        this.measureDefinitionService.updateMeasureDefinitionConfiguration(updateMeasureConfig).subscribe((response: IEntityCreatedResponse) => {
          this.processing = false;
          this.submittedConfiguration.emit(response);
        },
        error => {
          this.snackBar.open(`Please check for errors: ${error.statusText}`, '', {
              duration: 2500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'center',
              verticalPosition: 'top'
            });
          });
      }
    }
    else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 2500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'center',
        verticalPosition: 'top'
      });
    }
  }

}
