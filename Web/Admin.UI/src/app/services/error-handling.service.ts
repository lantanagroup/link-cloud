import { Injectable } from "@angular/core";
import { ToastrService } from "ngx-toastr";
import { throwError } from "rxjs/internal/observable/throwError";

@Injectable({
    providedIn: 'root'
  })
  export class ErrorHandlingService {

  constructor(private toastr: ToastrService) { }


  private sanitizeErrorMessage(message: string): string {
    // Remove sensitive information like stack traces, URLs, etc.
    return message.replace(/(?:https?|ftp):\/\/[\n\S]+/g, '[URL]')
      .replace(/\b(?:\d{1,3}\.){3}\d{1,3}\b/g, '[IP]');
  }


  handleError(err: any) {
        let errorMessage = '';

        if(err.error && err.error.detail && err.error.traceId) 
        {
          errorMessage = `${this.sanitizeErrorMessage(err.error.detail)} - ${err.error.traceId}`;
        }
        else
        {
          // If err.error is not available, fallback to err.message or a generic message
          if (err.message) {
            errorMessage = this.sanitizeErrorMessage(err.message);
          } else {
            errorMessage = 'An unknown error occurred';
          }
        }      

        this.toastr.error(errorMessage, 'Error', {
          timeOut: 5000,
          positionClass: 'toast-bottom-full-width',
          closeButton: true,
          progressBar: true,
          tapToDismiss: false,
          progressAnimation: 'decreasing'
        });

        err.message = errorMessage;
        
        return throwError(() => err);

      }

  }
