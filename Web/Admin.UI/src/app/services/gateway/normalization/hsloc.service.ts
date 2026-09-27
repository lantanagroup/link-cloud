import {Injectable} from '@angular/core';
import {HttpClient, HttpErrorResponse} from '@angular/common/http';
import {Observable, catchError} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {ErrorHandlingService} from '../../error-handling.service';

export interface Hsloc {
  id: string;
  hslocCode: string;
  cdcCode: string;
  shortDescription: string;
  longDescription: string;
  version: string;
  isActive: boolean;
}

@Injectable({providedIn: 'root'})
export class HslocService {
  constructor(private http: HttpClient, private appConfig: AppConfigService, private errorHandler: ErrorHandlingService) {}

  private get url(): string {
    return `${this.appConfig.config?.baseApiUrl}/normalization/HSLOC`;
  }

  getAll(): Observable<Hsloc[]> {
    return this.http.get<Hsloc[]>(this.url, {params: {includeInactive: true}})
      .pipe(catchError(error => this.errorHandler.handleError(error)));
  }

  update(oldVersion: string, newVersion: string, file: File): Observable<void> {
    const body = new FormData();
    body.append('OldVersion', oldVersion);
    body.append('NewVersion', newVersion);
    body.append('CsvFile', file);
    return this.http.put<void>(this.url, body)
      .pipe(catchError(error => this.errorHandler.handleError(error)));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`).pipe(
      catchError((error: HttpErrorResponse) => {
        const detail = error.error?.detail;
        return this.errorHandler.handleError(new HttpErrorResponse({
          error: {
            title: 'Unable to delete the HSLOC code',
            detail: typeof detail === 'string' && detail.includes('FK_FacilityLocationLocalCodeMapping_HSLOC')
              ? 'This HSLOC code cannot be deleted because it is used by one or more facility location mappings. Update or remove those mappings before deleting this code.'
              : 'Unable to delete the HSLOC code. Please try again. If the problem persists, contact your administrator.',
            traceId: error.error?.traceId
          },
          headers: error.headers,
          status: error.status,
          statusText: error.statusText,
          url: error.url ?? undefined
        }));
      })
    );
  }
}