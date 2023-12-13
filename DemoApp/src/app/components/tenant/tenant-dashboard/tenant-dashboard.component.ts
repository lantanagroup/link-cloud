import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IFacilityConfigModel } from '../../../interfaces/tenant/facility-config-model.interface';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FacilityConfigDialogComponent } from '../facility-config-dialog/facility-config-dialog.component';

@Component({
  selector: 'demo-tenant-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule
  ],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss']
})
export class TenantDashboardComponent implements OnInit {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  facilities: IFacilityConfigModel[] = []

  displayedColumns: string[] = [ "facilityId", 'facilityName', 'scheduledTasks' ];
  dataSource = new MatTableDataSource<IFacilityConfigModel>(this.facilities);

   //search parameters

  constructor(private tenantService: TenantService, private dialog: MatDialog, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.getFacilities();    
  }

  getFacilities() {
    this.tenantService.listFacilities('', '').subscribe((facilities: IFacilityConfigModel[]) => {
      this.facilities = facilities;      
    });
  }  

  showCreateFacilityDialog(): void {
    this.dialog.open(FacilityConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Create a facility configuration', viewOnly: false, facilityConfig: null }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {
          this.getFacilities();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }
}
