import { AfterContentInit, AfterViewInit, Component } from '@angular/core';

import { LoadingService } from '../../../services/loading.service';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@Component({
  selector: 'app-loading-indicator',
  standalone: true,
  imports: [
    MatProgressBarModule
],
  templateUrl: './loading-indicator.component.html',
  styleUrls: ['./loading-indicator.component.scss']
})
export class LoadingIndicatorComponent implements AfterViewInit {
  loading: boolean = false;

  constructor(private loadingService: LoadingService) {
  }

  ngAfterViewInit() {
    this.loadingService.isLoading.subscribe((loadingStatus) => {
      this.loading = loadingStatus;
    });
  }

}
