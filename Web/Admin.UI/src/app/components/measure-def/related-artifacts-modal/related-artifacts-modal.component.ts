import {Component, Inject, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule} from '@angular/material/dialog';
import {MatTableModule} from '@angular/material/table';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {CommonModule} from '@angular/common';
import {MeasureDefinitionService} from '../../../services/gateway/measure-definition/measure.service';
import {IRelatedArtifact} from '../../../interfaces/measure-definition/related-artifact.interface';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';

@Component({
  selector: 'app-related-artifacts-modal',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatTooltipModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './related-artifacts-modal.component.html',
  styleUrls: ['./related-artifacts-modal.component.scss']
})
export class RelatedArtifactsModalComponent implements OnInit {
  relatedArtifacts: IRelatedArtifact[] = [];
  displayedColumns: string[] = ['name', 'version', 'url', 'found'];
  loading = true;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { id: string },
    private measureDefinitionService: MeasureDefinitionService
  ) {}

  ngOnInit(): void {
    this.measureDefinitionService.getRelatedArtifacts(this.data.id).subscribe({
      next: (artifacts) => {
        this.relatedArtifacts = artifacts;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  getLibraryList(libraries?: { [key: string]: string }): string {
    if (!libraries) return '';
    return Object.entries(libraries).map(([uri, name]) => `${name} (${uri})`).join('\n');
  }
}
