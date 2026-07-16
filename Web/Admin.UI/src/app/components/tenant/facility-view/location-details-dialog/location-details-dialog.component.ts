import {Component, Inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {MAT_DIALOG_DATA, MatDialogModule} from '@angular/material/dialog';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {DataAcquisitionService} from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import {SnackbarHelper} from '../../../../services/snackbar-helper';
import {IOrganizationLocationMappingModel} from '../../../../interfaces/data-acquisition/organization-location-mapping-model.interface';

export interface LocationDetailsDialogData {
  locationMappingId: number;
}

@Component({
  selector: 'app-location-details-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule
  ],
  templateUrl: './location-details-dialog.component.html',
  styleUrl: './location-details-dialog.component.scss'
})
export class LocationDetailsDialogComponent implements OnInit {

  location: IOrganizationLocationMappingModel | null = null;
  isLoading = false;
  errorMessage: string | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: LocationDetailsDialogData,
    private dataAcquisitionService: DataAcquisitionService,
    private snackBar: MatSnackBar
  ) {}

  // Copies a value (e.g. the locationMappingId DB key) to the clipboard for log/DB lookups.
  copyToClipboard(value: string | number, label: string = 'Value'): void {
    navigator.clipboard.writeText(String(value))
      .then(() => SnackbarHelper.showSuccessMessage(this.snackBar, `${label} copied to clipboard.`))
      .catch(() => SnackbarHelper.showErrorMessage(this.snackBar, 'Unable to copy to clipboard.'));
  }

  ngOnInit(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.dataAcquisitionService.getLocationMappingById(this.data.locationMappingId).subscribe({
      next: (result) => {
        this.location = result;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.message || 'Failed to load location details.';
        this.isLoading = false;
      }
    });
  }
}
