import { Component, Inject, OnInit } from '@angular/core';

import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-delete-item-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule
],
  templateUrl: './delete-item-dialog.component.html',
  styleUrls: ['./delete-item-dialog.component.scss']
})
export class DeleteItemDialogComponent implements OnInit {
  dialogTitle: string = 'Are you sure you want to delete this item';
  dialogMessage: string = '';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, dialogMessage: string },
    private dialogRef: MatDialogRef<DeleteItemDialogComponent>) { }

  ngOnInit() {
    this.dialogTitle = this.data.dialogTitle;
    this.dialogMessage = this.data.dialogMessage;
  }

  confirmDeletion() {
    this.dialogRef.close(true);
  }

}
