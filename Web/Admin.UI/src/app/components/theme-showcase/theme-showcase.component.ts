import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatInputModule } from '@angular/material/input';
import { MatExpansionModule } from '@angular/material/expansion';
import { OverlayContainer } from '@angular/cdk/overlay';
import { ThemeShowcaseDialogComponent } from './theme-showcase-dialog/theme-showcase-dialog.component';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@Component({
  selector: 'app-theme-showcase',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatCardModule,
    MatIconModule,
    MatDialogModule,
    MatChipsModule,
    MatProgressBarModule
  ],
  templateUrl: './theme-showcase.component.html',
  styleUrls: ['./theme-showcase.component.scss']
})
export class ThemeShowcaseComponent {
  loading: boolean = true;

  constructor(private dialog: MatDialog, private overlay: OverlayContainer) { }

  showDialog(): void {
    this.dialog.open(ThemeShowcaseDialogComponent,
      {
        width: '500px'
      });
  }

}
