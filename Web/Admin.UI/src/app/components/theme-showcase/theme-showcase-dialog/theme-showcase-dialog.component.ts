import { Component } from '@angular/core';

import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-theme-showcase-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  templateUrl: './theme-showcase-dialog.component.html',
  styleUrls: ['./theme-showcase-dialog.component.css']
})
export class ThemeShowcaseDialogComponent {

}
