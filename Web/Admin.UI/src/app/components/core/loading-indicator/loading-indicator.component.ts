import { AfterContentInit, AfterViewInit, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoaderService } from '../../../services/loading.service';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@Component({
  selector: 'app-loading-indicator',
  standalone: true,
  imports: [
    CommonModule,
    MatProgressBarModule
  ],
  templateUrl: './loading-indicator.component.html',
  styleUrls: ['./loading-indicator.component.scss']
})
export class LoadingIndicatorComponent implements AfterViewInit {
  loading: boolean = false;

  constructor(private loadingService: LoaderService) {
  }

  ngAfterViewInit() {
    this.loadingService.isLoading.subscribe((loadingStatus) => {
      this.loading = loadingStatus;
    });
  }

}
