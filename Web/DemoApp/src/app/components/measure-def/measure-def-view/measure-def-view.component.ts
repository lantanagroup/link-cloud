import { Component, OnInit,Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { MeasureDefinitionDialogComponent } from '../measure-def-config-dialog/measure-def-config-dialog.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MeasureDefinitionFormComponent } from '../measure-def-config-form/measure-def-config-form.component';
import { ReactiveFormsModule } from '@angular/forms';
import { FormMode } from '../../../models/FormMode.enum';
import { MeasureDefinitionService } from '../../../services/gateway/measure-definition/measure.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityDeletedResponse } from '../../../interfaces/entity-deleted-response.interface';
import {MatTooltipModule} from "@angular/material/tooltip";

@Component({
  selector: 'app-measure-def-view',
  standalone: true,
  templateUrl: './measure-def-view.component.html',
  styleUrls: ['./measure-def-view.component.scss'],
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    RouterLink,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    MeasureDefinitionFormComponent,
    MatCardModule,
    MatTooltipModule
  ]
})
export class MeasureDefViewComponent implements OnInit {

  measureDefConfig!: IMeasureDefinitionConfigModel;
  facilityConfigFormViewOnly: boolean = true;
  facilityConfigFormIsInvalid: boolean = false;
  measureDefId !: string;

  @Output() deletedConfiguration = new EventEmitter<IEntityDeletedResponse>();

  constructor(
    private route: ActivatedRoute,
    private dialog: MatDialog,
    private measureDefinitionService: MeasureDefinitionService,
    private snackBar: MatSnackBar,
    private router: Router) { }

  ngOnInit() {
    this.route.params.subscribe(params => {
      this.measureDefId = params['measureDefId'];
      this.loadMeasureDefConfig();
    });
  }


  showMeasureConfigDialog(): void {
    this.dialog.open(MeasureDefinitionDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Edit Measure Config', formMode: FormMode.Edit, viewOnly: false, measureDefConfig: this.measureDefConfig }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {
          this.loadMeasureDefConfig();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }


  //load facility configurations
  loadMeasureDefConfig(): void {
    this.measureDefinitionService.getMeasureDefinitionConfiguration(this.measureDefId).subscribe((data: IMeasureDefinitionConfigModel)=> {
      this.measureDefConfig = data;
    });
  }

  //delete measure definition configurations
  deleteMeasureDefinitionConfiguration(): void {
    this.measureDefinitionService.deleteMeasureDefinitionConfiguration(this.measureDefId).subscribe(() => {
      let reportDeletedResponse: IEntityDeletedResponse = { id: this.measureDefId, message: "Measure Definition Configuration Deleted" };
      this.deletedConfiguration.emit(reportDeletedResponse);
      this.router.navigateByUrl('/measure-def', { state: reportDeletedResponse });
    });
  }

}
