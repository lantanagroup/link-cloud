import {Injectable} from "@angular/core";
import {ToastrService} from "ngx-toastr";
import {throwError} from "rxjs/internal/observable/throwError";

interface IProblemDetails {
  title?: unknown;
  status?: unknown;
  detail?: unknown;
  traceId?: unknown;
}

interface INormalizedError {
  title: string;
  detail: string;
}

@Injectable({
  providedIn: 'root'
})
export class ErrorHandlingService {

  private readonly normalizedErrors = new WeakMap<object, INormalizedError>();

  constructor(private toastr: ToastrService) {
  }

  private sanitizeErrorMessage(message: string): string {
    // Remove sensitive information like stack traces, URLs, etc.
    return message.replace(/(?:https?|ftp):\/\/[\n\S]+/g, '[URL]')
      .replace(/\b(?:\d{1,3}\.){3}\d{1,3}\b/g, '[IP]');
  }

  private getTextValue(value: unknown): string | undefined {
    return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
  }

  private sanitizeText(value: unknown, fallback: unknown): string {
    const text = this.getTextValue(value) ?? this.getTextValue(fallback) ?? 'An unknown error occurred';

    return this.sanitizeErrorMessage(text);
  }

  private getHttpStatus(value: unknown): number | undefined {
    return typeof value === 'number' && Number.isInteger(value) && value >= 100 && value <= 599
      ? value
      : undefined;
  }

  private getProblemDetails(err: any): INormalizedError | undefined {
    const problemDetails = err?.error as IProblemDetails | undefined;

    if (!problemDetails || typeof problemDetails !== 'object' || Array.isArray(problemDetails)) {
      return undefined;
    }

    const titleValue = this.getTextValue(problemDetails.title);
    const detailValue = this.getTextValue(problemDetails.detail);
    const traceIdValue = this.getTextValue(problemDetails.traceId);
    const status = this.getHttpStatus(problemDetails.status) ?? this.getHttpStatus(err?.status);

    if (!titleValue && status === undefined && !detailValue && !traceIdValue) {
      return undefined;
    }

    const title = this.sanitizeText(titleValue, 'Request failed');
    const detail = this.sanitizeText(detailValue, err?.message);
    const traceId = traceIdValue ? this.sanitizeText(traceIdValue, '') : undefined;

    return {
      title: status === undefined ? title : `${title} (${status})`,
      detail: traceId ? `${detail}\nTrace ID: ${traceId}` : detail
    };
  }

  private normalizeError(err: any, fallbackDetail: string): INormalizedError {
    const problemDetails = this.getProblemDetails(err);

    if (problemDetails) {
      return problemDetails;
    }

    return {
      title: 'Error',
      detail: this.sanitizeText(err?.message, fallbackDetail)
    };
  }

  formatError(err: any, fallbackDetail: string): string {
    const normalizedError = err && typeof err === 'object'
      ? this.normalizedErrors.get(err) ?? this.normalizeError(err, fallbackDetail)
      : this.normalizeError(err, fallbackDetail);

    return `${normalizedError.title}: ${normalizedError.detail}`;
  }

  handleError(err: any, genericToaster: boolean = true) {
    const normalizedError = this.normalizeError(err, 'An unknown error occurred');
    const rethrownError = err && typeof err === 'object' ? err : new Error(normalizedError.detail);
    const errorMessage = normalizedError.detail;
    const errorTitle = normalizedError.title;

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

    this.normalizedErrors.set(rethrownError, normalizedError);

    rethrownError.message = errorMessage;

    return throwError(() => rethrownError);

  }

}
