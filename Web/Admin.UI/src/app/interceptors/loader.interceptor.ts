import {Injectable} from '@angular/core';
import {HttpEvent, HttpHandler, HttpInterceptor, HttpRequest} from '@angular/common/http';
import {catchError, finalize, Observable, throwError, timeout} from 'rxjs';
import {LoadingService} from '../services/loading.service';


@Injectable()
export class LoaderInterceptor implements HttpInterceptor {
  private readonly DEFAULT_TIMEOUT = 30000;

  constructor(public loaderService: LoadingService) { }

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {

    const skipLoading = request.headers.get('X-Skip-Loading');

    // Remove the header
    const req = request.clone({
      headers: request.headers.delete('X-Skip-Loading')
    });

    if (!skipLoading) {
      this.loaderService.show();
     }

    return next.handle(req).pipe(
      timeout(this.DEFAULT_TIMEOUT),
      catchError((err) => {
        if (err.name === 'TimeoutError') {
          console.error(`Request for ${request.url} timed out after ${this.DEFAULT_TIMEOUT}ms`);
        }
        return throwError(() => err);
      }),
      finalize(() => {
        if(!skipLoading)
          {
            this.loaderService.hide()
          }
      })
    );
  }
}
