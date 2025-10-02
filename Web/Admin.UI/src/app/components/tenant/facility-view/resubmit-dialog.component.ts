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

export interface ResubmitDialogData {
  facilityId: string;
  reportId: string;
  // add any other data you pass when opening the dialog
}

@Component({
  selector: 'app-resubmit-dialog',
  templateUrl: './resubmit-dialog.component.html',
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
      bypassSubmission: [false], // <-- the checkbox control (boolean)
      comment: [''],
    });
  }

  onCancel(): void {
    this.dialogRef.close(); // no result
  }

  onSubmit(): void {
    if (this.isSubmitting) return;
    this.isSubmitting = true;

    // Prepare result to return to parent
    const result = {
      bypassSubmission: this.form.value.bypassSubmission,
      reportId: this.data.reportId,
    };

    // If you need to call a service from inside the dialog, you can do it here.
    // For a simpler pattern, just return the result and let parent call the service.
    this.dialogRef.close(result);
  }
}
