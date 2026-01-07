
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
import {
  IMeasureDefinitionConfigModel
} from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import {MeasureDefinitionService} from '../../../services/gateway/measure-definition/measure.service';
import {FileUploadComponent} from '../../core/file-upload/file-upload.component';
import {BundleIdValidator} from '../../validators/BundleIdValidator';
import {UrlOrBundleValidator} from '../../validators/UrlOrBundleValidator';
import {MatButtonModule} from "@angular/material/button";
import {MatCard, MatCardActions, MatCardContent, MatCardTitle} from "@angular/material/card";
import {MatTableModule} from "@angular/material/table";


@Component({
  selector: 'app-measure-def-config-form',
  standalone: true,
  providers: [
    UrlOrBundleValidator,
    BundleIdValidator
  ],
  imports: [
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
    MatButtonModule,
    MatCard,
    MatCardActions,
    MatCardContent,
    MatCardTitle,
    MatTableModule
],
  templateUrl: './measure-def-config-form.component.html',
  styleUrls: ['./measure-def-config-form.component.scss']
})
export class MeasureDefinitionFormComponent implements OnInit {

  configForm!: any;
  fileName = "";
  errorMessage: string = '';
  measureDefinitions: IMeasureDefinitionConfigModel[] = [];
  displayedColumns: string[] = ['id', 'version'];

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

  loadMeasureDefinitions(): void {
    this.measureDefinitionService.getMeasureDefinitionConfigurations().subscribe((measureDefinitions: IMeasureDefinitionConfigModel[]) => {
      this.measureDefinitions = measureDefinitions;
    });
  }

  loadFile(file: File | null) {
    if (file) {
      const reader = new FileReader();
      reader.readAsText(file);
      const fileName = file.name.toLowerCase();
      if (fileName.endsWith('.json')) {
        this.errorMessage = '';
      } else {
        this.errorMessage = 'Please upload a valid JSON file (with .json extension).';
        return;
      }
      reader.onload = (event: ProgressEvent<FileReader>) => {
        try {
          const fileContent = reader.result as string;
          const jsonData = JSON.parse(fileContent);

          this.bundle.setValue(jsonData);
          this.bundleId.setValue(jsonData.id || '');
        } catch (error) {
          throw new Error('Invalid JSON file format.');
        }
      };
      reader.onerror = () => {
        throw new Error('Error reading the file.');
      };
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
    this.loadMeasureDefinitions();
  }

  submitConfiguration(): void {
    if (this.configForm.status == 'VALID') {

      console.log('Submitting form:', this.configForm.value);

      let createMeasureConfig: IMeasureDefinitionConfigModel = {
        'id': this.bundleId.value,
        'bundle': this.bundle.value
      };

      this.measureDefinitionService.updateMeasureDefinitionConfiguration(createMeasureConfig).subscribe({
        next: () => {
          this.snackBar.open(`Successfully uploaded measure definition`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.clearForm();
          this.loadMeasureDefinitions();
        },
        error: (error) => {
          const errorMessage = error?.error?.message || error?.statusText || 'Unknown error occurred';
          this.snackBar.open(`Please check for errors: ${errorMessage}`, '', {
            duration: 5000,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          })
        }
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
