import { Injectable } from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';


@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(private router: Router) { }

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(request).pipe(catchError(err => {

      const isLoginCallback = this.router.url == "/login";

      // Skip interceptor logic for  login callback
      if (isLoginCallback) {
        return throwError(() => err);
      }

      if ([403].includes(err.status)) {
        // route to unauthorized
        this.router.navigate(['/unauthorized']);
      }
      else if([401].includes(err.status)){
        // route to login
        this.router.navigate(['/login']);
      }
      
      return throwError(() => err);
    }))
  }
}
