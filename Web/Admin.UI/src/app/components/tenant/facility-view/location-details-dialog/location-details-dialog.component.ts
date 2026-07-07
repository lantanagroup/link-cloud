import {Component, Inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {MAT_DIALOG_DATA, MatDialogModule} from '@angular/material/dialog';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {DataAcquisitionService} from '../../../../services/gateway/data-acquisition/data-acquisition.service';
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
    MatProgressSpinnerModule
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
    private dataAcquisitionService: DataAcquisitionService
  ) {}

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
