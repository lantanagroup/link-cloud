import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { FacilityConfigFormComponent } from '../facility-config-form/facility-config-form.component';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FacilityConfigDialogComponent } from '../facility-config-dialog/facility-config-dialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';


@Component({
  selector: 'app-facility-view',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    RouterLink,   
    MatDialogModule,
    FacilityConfigFormComponent
  ],
  templateUrl: './facility-view.component.html',
  styleUrls: ['./facility-view.component.scss']
})
export class FacilityViewComponent implements OnInit {
  facilityId: string = '';
  facilityConfig!: IFacilityConfigModel;
  facilityConfigFormViewOnly: boolean = true;
  facilityConfigFormIsInvalid: boolean = false;

  constructor(private route: ActivatedRoute, private tenantService: TenantService, private dialog: MatDialog, private snackBar: MatSnackBar) { }

  ngOnInit() {
    this.route.params.subscribe(params => {
      this.facilityId = params['id'];
      this.tenantService.getFacilityConfiguration(this.facilityId).subscribe((data: IFacilityConfigModel) => {
        this.facilityConfig = data;
      }); 
    });    
  }  

  showFacilityDialog(): void {
    this.dialog.open(FacilityConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Edit facility', viewOnly: false, facilityConfig: this.facilityConfig }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {          
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