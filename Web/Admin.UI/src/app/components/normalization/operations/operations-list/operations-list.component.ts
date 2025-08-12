import {Component, Input, OnInit} from '@angular/core';
import {NgForOf, NgIf} from "@angular/common";
import {MatButton, MatIconButton} from "@angular/material/button";
import {MatDialog} from "@angular/material/dialog";
import {MatIcon} from "@angular/material/icon";
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {OperationDialogComponent} from "../operation-dialog/operation-dialog.component";
import {FormMode} from "../../../../models/FormMode.enum";
import {SnackbarHelper} from "../../../../services/snackbar-helper";
import {ReactiveFormsModule} from "@angular/forms";
import {OperationJsonDialogComponent} from "./operation-json-dialog-component";
import {MatTooltip} from "@angular/material/tooltip";
import {MatPaginatorModule, PageEvent} from "@angular/material/paginator";
import {PaginationMetadata} from "../../../../models/pagination-metadata.model";
import {
  IOperationModel
} from "../../../../interfaces/normalization/operation-get-model.interface";
import {FaIconComponent} from "@fortawesome/angular-fontawesome";
import {faRotate} from "@fortawesome/free-solid-svg-icons";
import {
  DeleteConfirmationDialogComponent
} from "../../../core/delete-confirmation-dialog/delete-confirmation-dialog.component";
import {OperationsSequenceComponent} from "../operations-sequence/operations-sequence.component";
import {TestOperationComponent} from "../test-operation/test-operation.component";

@Component({
  selector: 'app-operations-list',
  imports: [
    MatIcon,
    MatTableModule,
    NgForOf,
    NgIf,
    ReactiveFormsModule,
    MatIconButton,
    MatTooltip,
    MatPaginatorModule,
    FaIconComponent,
    MatButton
  ],
  templateUrl: './operations-list.component.html',
  styleUrl: './operations-list.component.scss'
})
export class OperationsListComponent implements OnInit {

  operations: IOperationModel[] = [];

  displayedColumns = ['operationType', 'operationId', 'name', 'description', 'resourceTypes', 'isDisabled', 'operationJson', 'actions'];

  dataSource = new MatTableDataSource<IOperationModel>(this.operations);

  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  @Input() facilityId: string = "";

  @Input() set items(operations: IOperationModel[]) {
    this.operations = operations;
  }

  protected readonly JSON = JSON;

  constructor(private dialog: MatDialog, private snackBar: MatSnackBar, private operationService: OperationService) {
  }

  ngOnInit() {
    this.loadOperations();
  }

  showOperationDialog(operation: IOperationModel) {
    this.dialog.open(OperationDialogComponent,
      {
        width: '50vw',
        maxWidth: '50vw',
        data: {
          dialogTitle: 'Edit ' + this.toDescription(operation.operationType),
          formMode: FormMode.Edit,
          operationType: operation.operationType,
          viewOnly: false,
          operation: operation,
        },
        disableClose: true
      }).afterClosed().subscribe(res => {
      if (res) {
        SnackbarHelper.showSuccessMessage(this.snackBar, res);
        this.loadOperations();
      }
    });
  }

  onRefresh(): void {
    this.loadOperations();
  }

  toDescription(enumValue: string): string {
    // Insert a space before each uppercase letter that is preceded by a lowercase letter or number
    return enumValue.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadOperations();
  }

  loadOperations() {
    this.operationService.getOperationsByFacility(
      this.facilityId, this.paginationMetadata.pageSize,  this.paginationMetadata.pageNumber
    ).subscribe({
      next: (operationsSearch) => {
       const transformed = this.transformOperations(operationsSearch.records);
        this.operations = transformed;
        this.dataSource.data = transformed;
        this.paginationMetadata = operationsSearch.metadata;
      },
      error: (error) => {
        console.error('Error loading operations:', error);
      }
    });
  }

  transformOperations(operations: IOperationModel[]) {
    return operations.map(({operationResourceTypes = [], ...rest}) => ({
      ...rest,
      operationResourceTypes,
      resourceTypes: operationResourceTypes
        .map(r => r.resource?.resourceName)
        .filter((name): name is string => !!name), // filter out undefined/null
      showJson: false
    }));
  }

  openJsonDialog(operation: any): void {
    this.dialog.open(OperationJsonDialogComponent, {
      width: '600px',
      data: operation
    });
  }

  onDelete(row: IOperationModel): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: `Are you sure you want to delete this operation?`
      }
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.deleteOperation(row);
      }
    });
  }

  deleteOperation(operation: IOperationModel) {
    if (operation.facilityId !== null) {
      this.operationService.deleteOperationByFacility(operation.facilityId, operation.id)
        .subscribe({
          next: () => {
            this.loadOperations();
          },
          error: (err) => {
            SnackbarHelper.showErrorMessage(this.snackBar, err.message);
            console.error(err);
          }
        });
    }
  }

  openTestDialog(op: IOperationModel): void {
    this.dialog.open(TestOperationComponent, {
      width: '90vw',
      height: '100vh',
      maxWidth: '100vw',
      maxHeight: '100vh',
      panelClass: 'large-dialog',
      data: {
        operation: op
      },
      disableClose: true
    });
  }

  openOperationSequenceDialog(): void {
    this.dialog.open(OperationsSequenceComponent, {
      width: '80vw',
      height: '80vh',
      maxWidth: '90vw',
      maxHeight: '90vh',
      panelClass: 'large-dialog',
      data: {facilityId: this.facilityId},
      disableClose: true
    });
  }

  protected readonly faRotate = faRotate;
}
