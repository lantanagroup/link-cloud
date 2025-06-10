import {Component, Input, OnInit} from '@angular/core';
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {JsonPipe, NgForOf, NgIf, TitleCasePipe} from "@angular/common";
import {MatButton, MatIconButton} from "@angular/material/button";
import {MatDialog} from "@angular/material/dialog";
import {MatIcon} from "@angular/material/icon";

import {

  MatTableDataSource, MatTableModule
} from "@angular/material/table";

import {MatTooltip} from "@angular/material/tooltip";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {OperationDialogComponent} from "../operation-dialog/operation-dialog.component";
import {FormMode} from "../../../../models/FormMode.enum";

@Component({
  selector: 'app-operations-list',
  imports: [
    JsonPipe,
    TitleCasePipe,
    MatIcon,
    MatTooltip,
    MatTableModule,
    MatIconButton,
    MatButton,
    NgForOf,
    NgIf

  ],
  templateUrl: './operations-list.component.html',
  styleUrl: './operations-list.component.scss'
})
export class OperationsListComponent implements OnInit {

  operations = new MatTableDataSource<IOperationModel>();
  displayedColumns = ['operationType', 'description', 'resourceTypes', 'isDisabled', 'operationJson'];

  @Input() facilityId: string = "";

  @Input() set items(operations: IOperationModel[]) {

    this.operations.data = operations.map(({ Resources, ...rest }) => ({
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

  protected readonly JSON = JSON;
}
