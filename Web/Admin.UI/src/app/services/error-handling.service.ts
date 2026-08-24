import {Injectable} from "@angular/core";
import {ToastrService} from "ngx-toastr";
import {throwError} from "rxjs/internal/observable/throwError";

interface IProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ErrorHandlingService {


  constructor(private toastr: ToastrService) {
  }

  private sanitizeErrorMessage(message: string): string {
    // Remove sensitive information like stack traces, URLs, etc.
    return message.replace(/(?:https?|ftp):\/\/[\n\S]+/g, '[URL]')
      .replace(/\b(?:\d{1,3}\.){3}\d{1,3}\b/g, '[IP]');
  }

  private getProblemDetails(err: any): { title: string; detail: string } | undefined {
    const problemDetails = err?.error as IProblemDetails | undefined;

    if (!problemDetails || typeof problemDetails !== 'object' ||
      (!problemDetails.title && problemDetails.status === undefined && !problemDetails.detail)) {
      return undefined;
    }

    const title = this.sanitizeErrorMessage(problemDetails.title ?? 'Request failed');
    const status = problemDetails.status ?? err?.status;
    const detail = this.sanitizeErrorMessage(problemDetails.detail ?? err?.message ?? 'An unknown error occurred');
    const traceId = problemDetails.traceId ? this.sanitizeErrorMessage(problemDetails.traceId) : undefined;

    return {
      title: status === undefined ? title : `${title} (${status})`,
      detail: traceId ? `${detail}\nTrace ID: ${traceId}` : detail
    };
  }

  handleError(err: any, genericToaster: boolean = true) {
    const problemDetails = this.getProblemDetails(err);
    let errorMessage: string;
    let errorTitle = 'Error';

    if (problemDetails) {
      errorMessage = problemDetails.detail;
      errorTitle = problemDetails.title;
    } else {
      if (err.message) {
        errorMessage = this.sanitizeErrorMessage(err.message);
      } else {
        errorMessage = 'An unknown error occurred';
      }
    }

    if (genericToaster) {
      this.toastr.error(errorMessage, errorTitle, {
        timeOut: 5000,
        positionClass: 'toast-bottom-full-width',
        closeButton: true,
        progressBar: true,
        tapToDismiss: false,
        progressAnimation: 'decreasing'
      });
    }

    err.message = errorMessage;

    return throwError(() => err);

  }

}
