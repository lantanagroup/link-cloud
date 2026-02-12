import {Component, Inject, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule} from '@angular/material/dialog';
import {MatTabsModule} from '@angular/material/tabs';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {CommonModule} from '@angular/common';
import {ValidationService} from '../../../services/gateway/validation/validation.service';
import {IPackageDetailsModel, IPackageResource} from '../../../interfaces/validation/package-details.interface';
import {TxDependenciesComponent} from '../tx-dependencies/tx-dependencies.component';

@Component({
  selector: 'app-package-details-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTabsModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatButtonModule,
    TxDependenciesComponent
  ],
  templateUrl: './package-details-dialog.component.html',
  styleUrls: ['./package-details-dialog.component.scss']
})
export class PackageDetailsDialogComponent implements OnInit {
  details?: IPackageDetailsModel;
  loading = false;
  errorMessage = '';

  resourcesData = new MatTableDataSource<IPackageResource>([]);
  displayedColumns: string[] = ['resourceType', 'id', 'name', 'version', 'url'];

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { package: string },
    private validationService: ValidationService
  ) {}

  ngOnInit(): void {
    this.loadDetails();
  }

  private loadDetails(): void {
    this.loading = true;
    this.errorMessage = '';

    this.validationService.getPackageDetails(this.data.package).subscribe({
      next: (details) => {
        this.details = details;
        this.resourcesData.data = details.resources ?? [];
        this.loading = false;
      },
      error: (error) => {
        console.error('Failed to load package details:', error);
        this.errorMessage = 'Failed to load package details.';
        this.loading = false;
      }
    });
  }
}
