/// <reference path="../measure-def-config-form/measure-def-config-form.component.ts" />
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { MeasureDefinitionDialogComponent } from '../measure-def-config-dialog/measure-def-config-dialog.component';
import { FormMode } from '../../../models/FormMode.enum';
import { MeasureDefinitionService } from '../../../services/gateway/measure-definition/measure.service';
import { IEntityDeletedResponse } from '../../../interfaces/entity-deleted-response.interface';

@Component({
  selector: 'app-measure-def-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    RouterLink
  ],
  templateUrl: './measure-def-dashboard.component.html',
  styleUrls: ['./measure-def-dashboard.component.scss']
})
export class MeasureDefinitionDashboardComponent implements OnInit {

  measureDefinitions: IMeasureDefinitionConfigModel[] = [];

  displayedColumns: string[] = ["measureDefinitionId", 'measureDefinitionName', 'url'];

  dataSource = new MatTableDataSource<IMeasureDefinitionConfigModel>(this.measureDefinitions);


  constructor(private dialog: MatDialog, private snackBar: MatSnackBar, private measureDefinitionService: MeasureDefinitionService) { }

  getMeasureDefinition() {
    return this.measureDefinitions;
  }

  getMeasureDefinitions() {
    this.measureDefinitionService.getMeasureDefinitionConfigurations().subscribe((measureDefinitions: IMeasureDefinitionConfigModel[]) => {
      this.measureDefinitions = measureDefinitions;
    });
  }

  ngOnInit(): void {
    this.getMeasureDefinitions();
  }

  showCreateMeasureDefinitionDialog() {
    this.dialog.open(MeasureDefinitionDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Create a measure definition configuration', formMode: FormMode.Create, viewOnly: false, measureDefConfig: { bundleId: "", bundleName: "", url: "" } }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {
          this.getMeasureDefinitions();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  onDeletedConfiguration(outcomeMessage: IEntityDeletedResponse) {
    console.log(outcomeMessage.message);
    this.getMeasureDefinitions();
  }

}
