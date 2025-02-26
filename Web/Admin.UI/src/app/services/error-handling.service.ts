import { HttpErrorResponse } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { throwError } from "rxjs/internal/observable/throwError";

@Injectable({
    providedIn: 'root'
  })
  export class ErrorHandlingService {


  private sanitizeErrorMessage(message: string): string {
    // Remove sensitive information like stack traces, URLs, etc.
    return message.replace(/(?:https?|ftp):\/\/[\n\S]+/g, '[URL]')
      .replace(/\b(?:\d{1,3}\.){3}\d{1,3}\b/g, '[IP]');
  }


  handleError(err: any) {
        let errorMessage = '';

        if (err instanceof ErrorEvent) {
          errorMessage = `An error occured: ${this.sanitizeErrorMessage(err.error.message)}`;
        }
        else {
          errorMessage = `Server returned code: ${err.status}, error message is: ${this.sanitizeErrorMessage(err.message)}`;
        }

        err.message = errorMessage;

        console.error(errorMessage);
        return throwError(() => err);

      }

  }
