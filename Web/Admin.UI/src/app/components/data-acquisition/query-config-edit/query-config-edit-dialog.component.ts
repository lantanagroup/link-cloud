import {Component, Inject, ViewChild} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from "@angular/material/dialog";
import {MatButtonModule} from "@angular/material/button";
import {MatIconModule} from "@angular/material/icon";
import {QueryConfigEditComponent} from "./query-config-edit.component";
import {QueryConfigModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";

@Component({
  selector: 'app-query-config-edit-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, QueryConfigEditComponent],
  template: `
    <h2 mat-dialog-title>{{data.dialogTitle}}</h2>
    <mat-dialog-content>
      <app-query-config-edit [config]="data.config" [viewOnly]="data.viewOnly" (configChanged)="onConfigChanged($event)"></app-query-config-edit>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      @if (!data.viewOnly) {
        <button mat-raised-button color="primary" (click)="save()">Save</button>
      }
    </mat-dialog-actions>
  `
})
export class QueryConfigEditDialogComponent {
  @ViewChild(QueryConfigEditComponent) editForm!: QueryConfigEditComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, config: QueryConfigModel, viewOnly: boolean },
    private dialogRef: MatDialogRef<QueryConfigEditDialogComponent>
  ) {}

  onConfigChanged(config: QueryConfigModel) {
    this.dialogRef.close(config);
  }

  save() {
    this.editForm.save();
  }
}
