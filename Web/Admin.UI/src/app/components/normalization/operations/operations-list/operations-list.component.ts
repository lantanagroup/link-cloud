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
import {SnackbarHelper} from "../../../../services/snackbar-helper";
import {MatCheckbox} from "@angular/material/checkbox";
import {ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-operations-list',
  imports: [
    JsonPipe,
    MatIcon,
    MatTableModule,
    MatButton,
    NgForOf,
    NgIf,
    MatCheckbox,
    ReactiveFormsModule

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
          dialogTitle: 'Edit ' + this.toDescription(operation.OperationType),
          formMode: FormMode.Edit,
          operationType: operation.OperationType,
          viewOnly: false,
          operation: operation
        }
      }).afterClosed().subscribe(res => {
      if (res) {
        SnackbarHelper.showSuccessMessage(this.snackBar, res);
        this.operationService.getOperationConfiguration(this.facilityId).subscribe({
          next: (operations: IOperationModel[]) => {
            this.operations.data = this.transformOperations(operations);
          },
          error: () => {
            SnackbarHelper.showErrorMessage(this.snackBar, 'Failed to load Operations Config for the facility, see error for details.');
          }
        });
      }
    });
  }

  toDescription(enumValue: string): string {
    // Insert a space before each uppercase letter that is preceded by a lowercase letter or number
    return enumValue.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  }


  private transformOperations(operations: IOperationModel[]): IOperationModel[] {
    return operations.map(({Resources, ...rest}) => ({
      ...rest,
      ResourceTypes: Resources?.map(r => r.ResourceName) ?? [],
      Resources,
      showJson: false
    }));
  }

  protected readonly JSON = JSON;
}
