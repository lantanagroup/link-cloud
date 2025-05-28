import {CommonModule} from '@angular/common';
import {Component, OnInit} from '@angular/core';
import {FormBuilder, ReactiveFormsModule} from '@angular/forms';
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
import {IEntityCreatedResponse} from '../../interfaces/entity-created-response.model';
import {FileUploadComponent} from '../core/file-upload/file-upload.component';

import {MatButtonModule} from "@angular/material/button";
import {ValidationService} from "../../services/gateway/validation/validation.service";
import {IValidationConfiguration} from "../../interfaces/validation/validation-configuration.interface";

@Component({
  selector: 'app-measure-def-config-form',
  standalone: true,
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
  templateUrl: './validation-config.component.html',
  styleUrls: ['./validation-config.component.scss']
})
export class ValidationConfigComponent implements OnInit {

  configForm!: any;
  fileName = "";
  errorMessage: string = '';

  constructor(private formBuilder: FormBuilder, private validationService: ValidationService, private snackBar: MatSnackBar) {

    this.configForm = this.formBuilder.group(
      {
        name: [""],
        content: [null]
      }
    );
  }

  get content() {
    return this.configForm.controls['content'];
  }

  get name() {
    return this.configForm.controls['name'];
  }

  loadFile(file: File | null) {
    if (file) {
      const reader = new FileReader();
      reader.readAsArrayBuffer(file); // Binary read for .tgz
      const fileName = file.name.toLowerCase();
      if (fileName.endsWith('.tgz')) {
        this.errorMessage = '';
      } else {
        this.errorMessage = 'Please upload a valid IG file (with .tgz extension).';
        return;
      }
      reader.onload = () => {
        this.content.setValue(file);
        const newFileName: string = fileName.replace(/\.[^\.]+$/, '');
        this.name.setValue(newFileName);
      };
      reader.onerror = () => {
        this.errorMessage = 'Error reading the TGZ file.';
      };
    } else {
      this.fileName = '';
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

      let vlidationConfig: IValidationConfiguration = {
        'type': 'PACKAGE',
        'name': this.name.value,
        'content': this.content.value
      };

      this.validationService.updateValidationConfiguration(vlidationConfig).subscribe((response: IEntityCreatedResponse) => {
          this.snackBar.open(`Successfully uploaded IG`, '', {
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
