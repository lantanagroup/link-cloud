import { Component, Inject } from '@angular/core';
import {FormBuilder, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle
} from '@angular/material/dialog';
import {MatCheckbox} from "@angular/material/checkbox";
import {MatButton} from "@angular/material/button";
import {faRotate} from "@fortawesome/free-solid-svg-icons";

export interface ResubmitDialogData {
  facilityId: string;
  reportId: string;
}

@Component({
  selector: 'app-resubmit-dialog',
  templateUrl: './resubmit-dialog.component.html',
  styleUrls: ['./resubmit-dialog.component.scss'],
  imports: [
    MatDialogActions,
    MatCheckbox,
    ReactiveFormsModule,
    MatDialogContent,
    MatDialogTitle,
    MatButton
  ]
})
export class ResubmitDialogComponent {
  form: FormGroup;
  isSubmitting = false;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<ResubmitDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ResubmitDialogData
  ) {
    this.form = this.fb.group({
      bypassSubmission: [false],
      comment: [''],
    });
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSubmit(): void {
    if (this.isSubmitting) return;
    this.isSubmitting = true;

    const result = {
      bypassSubmission: this.form.value.bypassSubmission,
      reportId: this.data.reportId,
    };

    this.dialogRef.close(result);
  }

  protected readonly faRotate = faRotate;
}
