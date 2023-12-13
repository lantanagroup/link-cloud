import { HttpErrorResponse } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { throwError } from "rxjs/internal/observable/throwError";

@Injectable({
    providedIn: 'root'
  })
  export class ErrorHandlingService {

    handleError(err: any) {
        let errorMessage = '';
    
        if (err.error instanceof ErrorEvent) {
          errorMessage = `An error occured: ${err.error.message}`;
        }
        else {
          errorMessage = `Server returned code: ${err.status}, error message is: ${err.message}`;
        }
    
        console.error(errorMessage);
        return throwError(() => errorMessage);
    
      }

  }