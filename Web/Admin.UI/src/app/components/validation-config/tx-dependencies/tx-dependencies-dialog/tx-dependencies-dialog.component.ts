import {Component, Inject} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule} from '@angular/material/dialog';
import {MatButtonModule} from '@angular/material/button';
import {TxDependenciesComponent} from '../tx-dependencies.component';

@Component({
  selector: 'app-tx-dependencies-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, TxDependenciesComponent],
  templateUrl: './tx-dependencies-dialog.component.html',
  styleUrls: ['./tx-dependencies-dialog.component.scss']
})
export class TxDependenciesDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { package: string }) {}
}
