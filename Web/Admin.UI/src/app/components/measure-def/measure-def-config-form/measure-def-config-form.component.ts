import {CommonModule} from '@angular/common';
import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {FormBuilder, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatChipsModule} from '@angular/material/chips';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatSelectModule} from '@angular/material/select';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatTooltipModule} from '@angular/material/tooltip';
import {IEntityCreatedResponse} from '../../../interfaces/entity-created-response.model';
import {IMeasureDefinitionConfigModel} from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import {MeasureDefinitionService} from '../../../services/gateway/measure-definition/measure.service';
import {FileUploadComponent} from '../../file-upload/file-upload.component';
import {BundleIdValidator} from '../../validators/BundleIdValidator';
import {UrlOrBundleValidator} from '../../validators/UrlOrBundleValidator';
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
export class MeasureDefinitionFormComponent implements OnInit {

  configForm!: any;

  fileName = "";


  constructor(private formBuilder: FormBuilder, private measureDefinitionService: MeasureDefinitionService, private bundleIdValidator: BundleIdValidator, private snackBar: MatSnackBar) {

    this.configForm = this.formBuilder.group(
      {
        bundleId: ["", Validators.required],
        bundle: [null]
      }
    );
  }

  validateBundleId(control: FormGroup) {
    return this.bundleIdValidator.invalidBundleId(control.value) ? {invalidBundleId: true} : null;
  }

  get invalidBundleId() {
    return this.configForm.hasError('invalidBundleId');
  }

  get bundleId() {
    return this.configForm.controls['bundleId'];
  }

  get bundle() {
    return this.configForm.controls['bundle'];
  }

  loadFile(file: any) {
    if (file) {
      this.bundle.setValue(file);
      this.bundleId.setValue(file["id"]);
    } else {
      this.fileName = '';
      this.bundleId.setValue('');
      this.bundle.setValue(null);
      this.configForm.reset();
    }
  }

  clearForm() {
    this.configForm.reset();
    this.fileName = '';
  }

  ngOnInit(): void {

  }

  submitConfiguration(): void {
    if (this.configForm.status == 'VALID') {

      console.log('Submitting form:', this.configForm.value);

      let createMeasureConfig: IMeasureDefinitionConfigModel = {
        'bundleId': this.bundleId.value,
        'bundle': this.bundle.value
      };
      this.measureDefinitionService.updateMeasureDefinitionConfiguration(createMeasureConfig).subscribe((response: IEntityCreatedResponse) => {
          this.snackBar.open(`Successfully uploaded measure definition`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        },
        error => {
          this.snackBar.open(`Please check for errors: ${error.statusText}`, '', {
            duration: 5000,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        });
    } else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 2500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'center',
        verticalPosition: 'top'
      });
    }
  }
}
