import {Component, Input, OnInit} from '@angular/core';
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {JsonPipe, NgForOf, NgIf} from "@angular/common";
import {MatButton} from "@angular/material/button";
import {MatDialog} from "@angular/material/dialog";
import {MatIcon} from "@angular/material/icon";

import {
  MatTableDataSource, MatTableModule
} from "@angular/material/table";

import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {OperationDialogComponent} from "../operation-dialog/operation-dialog.component";
import {FormMode} from "../../../../models/FormMode.enum";

@Component({
  selector: 'app-operations-list',
  imports: [
    JsonPipe,
    MatIcon,
    MatTableModule,
    MatButton,
    NgForOf,
    NgIf

  ],
  templateUrl: './operations-list.component.html',
  styleUrl: './operations-list.component.scss'
})
export class OperationsListComponent implements OnInit {

  operations = new MatTableDataSource<IOperationModel>();
  displayedColumns = ['operationType', 'description', 'resourceTypes', 'isDisabled', 'operationJson', 'actions'];

  @Input() facilityId: string = "";

  @Input() set items(operations: IOperationModel[]) {

    this.operations.data = operations.map(({Resources, ...rest}) => ({
      ...rest,
      ResourceTypes: Resources?.map(r => r.ResourceName) ?? [],
      Resources: Resources,
      showJson: false
    }));

  }

  constructor(private dialog: MatDialog, private snackBar: MatSnackBar, private operationService: OperationService) {
  }

  ngOnInit() {

  }

  showOperationDialog(operation: IOperationModel) {
    this.dialog.open(OperationDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Edit ' + operation.OperationType,
          formMode: FormMode.Edit,
          operationType: operation.OperationType,
          viewOnly: false,
          operation: operation
        }
      }).afterClosed().subscribe(res => {
      console.log(res);
      if (res) {
        this.operationService.getOperationConfiguration(this.facilityId).subscribe(
          (operations: IOperationModel[]) => {
            this.operations.data = operations.map(({Resources, ...rest}) => ({
              ...rest,
              ResourceTypes: Resources?.map(r => r.ResourceName) ?? [],
              Resources: Resources,
              showJson: false
            }));

          },
          error => {
            this.snackBar.open(
              `Failed to load Operations Config for the facility, see error for details.`,
              '',
              {
                duration: 3500,
                panelClass: 'error-snackbar',
                horizontalPosition: 'end',
                verticalPosition: 'top'
              }
            );
          }
        );
        this.snackBar.open(`${res}`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
        });
      }
    });
  }

  protected readonly JSON = JSON;
}
