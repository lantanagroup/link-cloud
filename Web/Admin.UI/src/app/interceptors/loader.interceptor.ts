import { Injectable } from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor } from '@angular/common/http';
import { finalize, Observable } from 'rxjs';
import { LoadingService } from '../services/loading.service';


@Injectable()
export class LoaderInterceptor implements HttpInterceptor {

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
      finalize(() => {
        if(!skipLoading)
          {
            this.loaderService.hide()
          }
      })
    );
  }
}
