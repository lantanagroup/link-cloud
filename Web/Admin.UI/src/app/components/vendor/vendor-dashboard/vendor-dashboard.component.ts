import {Component} from '@angular/core';
import {CommonModule} from '@angular/common';
import {PaginationMetadata} from "../../../models/pagination-metadata.model";
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {MatPaginatorModule, PageEvent} from "@angular/material/paginator";
import {MatExpansionModule} from "@angular/material/expansion";
import {FormsModule} from "@angular/forms";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatSelectModule} from "@angular/material/select";
import {MatInputModule} from "@angular/material/input";
import {MatIconModule} from "@angular/material/icon";
import {MatButtonModule} from "@angular/material/button";
import {MatTooltipModule} from "@angular/material/tooltip";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatChipsModule} from "@angular/material/chips";
import {MatDialog} from "@angular/material/dialog";
import {VendorConfigDialogComponent} from "../vendor-config-dialog/vendor-config-dialog.component";
import {FormMode} from "../../../models/FormMode.enum";
import {MatSnackBar} from "@angular/material/snack-bar";
import {DeleteConfirmationDialogComponent} from "../../core/delete-confirmation-dialog/delete-confirmation-dialog.component";
import {VendorService} from "../../../services/gateway/vendor/vendor.service";
import {IVendorConfigModel} from "../../../interfaces/vendor/vendor-config-model.interface";


@Component({
  selector: 'app-vendor-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatPaginatorModule,
    MatExpansionModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatToolbarModule,
    MatChipsModule
  ],
  templateUrl: './vendor-dashboard.component.html',
  styleUrls: ['./vendor-dashboard.component.scss']
})
export class VendorDashboardComponent {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  vendors: IVendorConfigModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  dataSource!: MatTableDataSource<IVendorConfigModel>;

  displayedColumns: string[] = ['name', 'Actions'];

  loading = false;
  error: string | null = null;

  constructor(private vendorService: VendorService, private dialog: MatDialog, private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<IVendorConfigModel>();
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getVendors();
  }

  getVendors() {
    this.loading = true;
    this.error = null;
    this.vendorService.getVendors().subscribe({
      next: (data) => {
        this.vendors = data;
      },
      error: (error) => {
        this.error = 'Failed to load vendors. Please try again.';
        this.loading = false;
        console.error('Error loading vendors:', error);
      }
    });
  }

  onDelete(row: any): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: `Are you sure you want to delete this vendor configuration?`
      }
    });
    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.deleteVendorConfig(row);
      }
    });
  }


  onAdd(): void {
    this.dialog.open(VendorConfigDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Vendor Configuration',
          formMode: FormMode.Create,
          viewOnly: false,
          vendorConfig: {},
        }
      }).afterClosed().subscribe(res => {
      if (res) {
        this.getVendors();
        this.snackBar.open(`Vendor Created`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  private deleteVendorConfig(row: any): void {
    console.log(`Deleted Vendor Config: ${JSON.stringify(row)}`);
    this.vendorService.deleteVendor(row.id).subscribe(() => {
      this.snackBar.open('Vendor configuration deleted successfully!', '', {
        duration: 3500,
        panelClass: 'success-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      this.getVendors();
    });
  }
}
