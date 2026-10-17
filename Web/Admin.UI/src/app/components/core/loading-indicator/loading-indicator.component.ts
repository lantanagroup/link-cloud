import { Component } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { Observable, delay } from 'rxjs';

import { LoadingService } from '../../../services/loading.service';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@Component({
  selector: 'app-loading-indicator',
  standalone: true,
  imports: [
    AsyncPipe,
    MatProgressBarModule
],
  templateUrl: './loading-indicator.component.html',
  styleUrls: ['./loading-indicator.component.scss']
})
export class LoadingIndicatorComponent {
  /**
   * LoaderInterceptor calls show() synchronously as a request is issued, so a component that
   * fetches from a lifecycle hook emits mid-change-detection — after this indicator, which
   * lives in the app shell, has already been checked. That flips the template's @if too late
   * and Angular reports NG0100 in dev mode. Deferring by a tick moves the flip into the next
   * pass; the async pipe then handles subscription and teardown.
   */
  readonly loading$: Observable<boolean>;

  constructor(private loadingService: LoadingService) {
    this.loading$ = this.loadingService.isLoading.pipe(delay(0));
  }
}
