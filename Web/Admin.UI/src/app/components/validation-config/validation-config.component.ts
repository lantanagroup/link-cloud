
import {Component, OnInit} from '@angular/core';
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
import {ValidationService} from "../../services/gateway/validation/validation.service";
import {IValidationConfiguration} from "../../interfaces/validation/validation-configuration.interface";
import {Artifact} from "../../interfaces/validation/artifact.interface";
import {MatCard, MatCardActions, MatCardContent, MatCardTitle} from "@angular/material/card";
import {MatList, MatListItem} from "@angular/material/list";
import {FileUploadComponent} from '../core/file-upload/file-upload.component';

import {MatButtonModule} from "@angular/material/button";

@Component({
  selector: 'app-validation-config',
  standalone: true,
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
    MatList,
    MatCardContent,
    MatCardTitle,
    MatListItem,
    MatFormFieldModule
],
  templateUrl: './validation-config.component.html',
  styleUrls: ['./validation-config.component.scss']
})
export class ValidationConfigComponent implements OnInit {

  configForm!: FormGroup;
  fileName = "";
  errorMessage: string = '';
  implementationGuides: Artifact[] = [];

  constructor(private formBuilder: FormBuilder, private validationService: ValidationService, private snackBar: MatSnackBar) {

    this.configForm = this.formBuilder.group(
      {
        name: ['', Validators.required],
        content: [null, Validators.required]
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
      const fileName = file.name.toLowerCase();
      if (fileName.endsWith('.tgz')) {
        reader.readAsArrayBuffer(file); // Binary read for .tgz
        this.errorMessage = '';
      } else {
        this.errorMessage = 'Please upload a valid IG file (with .tgz extension).';
        this.configForm.reset();
        return;
      }
      reader.onload = () => {
        this.content.setValue(reader.result as ArrayBuffer);
        const newFileName: string = fileName.replace(/\.[^\.]+$/, '');
        this.name.setValue(newFileName);
      };
      reader.onerror = () => {
        this.errorMessage = 'Error reading the TGZ file.';
      };
    } else {
      this.fileName = '';
      this.errorMessage = '';
      this.configForm.reset();
    }
  }

  clearForm() {
    this.configForm.reset();
    this.fileName = '';
  }

  ngOnInit(): void {
    this.loadIgs();
  }

  loadIgs(){
    this.validationService.getValidationConfiguration().subscribe((IGs: Artifact[]) => {
      this.implementationGuides = IGs;
    });

  }
  submitConfiguration(): void {
    if (this.configForm.valid) {
      console.log('Submitting form:', this.configForm.value);
      let validationConfig: IValidationConfiguration = {
        'type': 'PACKAGE',
        'name': this.name.value,
        'content': this.content.value
      };

      this.validationService.updateValidationConfiguration(validationConfig).subscribe({
        next: () => {
          this.snackBar.open(`Successfully uploaded IG`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.clearForm();
          this.loadIgs();
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
      // Mark all fields as touched to show validation errors
      this.configForm.markAllAsTouched();
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 2500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'center',
        verticalPosition: 'top'
      });
    }
  }
}
