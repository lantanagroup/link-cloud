import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IReportConfigModel } from '../../../interfaces/report/report-config-model.interface';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ReportService } from '../../../services/gateway/report/report.service';
import { ReportConfigDialogComponent } from '../report-config-dialog/report-config-dialog.component';
import { FormMode } from '../../../models/FormMode.enum';
import { ReportConfigFormComponent } from '../report-config-form/report-config-form.component';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from '../../../interfaces/entity-deleted-response.interface';
import { LinkAlertType } from '../../core/link-alert/link-alert-type.enum';
import { LinkAlertComponent } from '../../core/link-alert/link-alert.component';

@Component({
  selector: 'app-report-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    LinkAlertComponent,
    ReportConfigFormComponent
  ],
  templateUrl: './report-dashboard.component.html',
  styleUrls: ['./report-dashboard.component.scss']
})
export class ReportDashboardComponent implements OnInit {

  @Input() facilityId!: string;

  @Input() displayReportDashboard: boolean = false;

  reports: IReportConfigModel[] = [];

  linkNoConfigAlertType = LinkAlertType.info;
  showNoReportConfigAlert: boolean = false;
  noReportConfigAlertMessage = 'No report configuration found for this facility.';

  constructor(private reportService: ReportService, private dialog: MatDialog, private snackBar: MatSnackBar) { }

  addReportConfig(): void {
    this.dialog.open(ReportConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Report Configuration', formMode: FormMode.Create, viewOnly: false, reportConfig: { facilityId: this.facilityId, reportType: "", bundlingType: "" } }
      }).afterClosed().subscribe(res => {
        console.log(`Dialog result: ${res}`);
        if (res) {
          this.onSubmittedConfiguration(res);
        };
        this.snackBar.open(`${res}`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });

      });
  }

  onSubmittedConfiguration(outcomeMessage: IEntityCreatedResponse | IEntityDeletedResponse) {
    console.log(`Queried reports after:  ${outcomeMessage.message}`);
    this.getReports();
  }

  getReports() {
    this.reportService.getReportConfigurations(this.facilityId).subscribe((reports: IReportConfigModel[]) => {
      this.reports = reports;
      if (this.reports.length > 0) {
        this.showNoReportConfigAlert = false;
      }
      else {
        this.showNoReportConfigAlert = true;
        if (this.displayReportDashboard) {
          this.addReportConfig();
          this.displayReportDashboard = false;
        }
      }
    },
      error => {
        if (error.status == 404) {
          this.snackBar.open(`No current report configuration found for facility ${this.facilityId}, please create one.`, '', {
            duration: 3500,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.showNoReportConfigAlert = true;
          if (this.displayReportDashboard) {
            this.addReportConfig();
            this.displayReportDashboard = false;
          }
        }
        else {
          this.snackBar.open(`Failed to load report configuration for the facility, see error for details.`, '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });

  }

  ngOnInit(): void {
    this.getReports();
  }

}
