import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable } from 'rxjs';
import { AppConfigService } from 'src/app/services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { IPagedSftpAcquisitionLogSummary } from './models/sftp-acquisition-log-summary';
import { SftpAcquisitionLog } from './models/sftp-acquisition-log';

@Injectable({
  providedIn: 'root'
})
export class SftpAcquisitionLogService {

  constructor(
    private http: HttpClient,
    private errorHandler: ErrorHandlingService,
    public appConfigService: AppConfigService
  ) { }

  get baseUrl(): string {
    return `${this.appConfigService.config?.baseApiUrl}/data/sftp-logs`;
  }

  getSftpAcquisitionLogs(
    facilityId: string | null,
    acquisitionType: string | null,
    status: string | null,
    sortBy: string | null,
    sortOrder: 'ascending' | 'descending' | null,
    pageNumber: number,
    pageSize: number,
    showLoadingIndicator: boolean = true
  ): Observable<IPagedSftpAcquisitionLogSummary> {

    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });

    let params: HttpParams = new HttpParams();
    params = params.set('pageNumber', (pageNumber + 1).toString()); // API uses 1-based paging
    params = params.set('pageSize', pageSize.toString());

    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    if (sortOrder) {
      params = params.set('sortOrder', sortOrder);
    }
    if (facilityId) {
      params = params.set('facilityId', facilityId);
    }
    if (acquisitionType) {
      params = params.set('acquisitionType', acquisitionType);
    }
    if (status) {
      params = params.set('status', status);
    }

    const options = showLoadingIndicator ? { params } : { params, headers };

    return this.http.get<IPagedSftpAcquisitionLogSummary>(this.baseUrl, options)
      .pipe(
        map((response: IPagedSftpAcquisitionLogSummary) => {
          response.metadata.pageNumber--; // Convert back to 0-based
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

  getSftpAcquisitionLog(id: string): Observable<SftpAcquisitionLog> {
    return this.http.get<SftpAcquisitionLog>(`${this.baseUrl}/${id}`)
      .pipe(
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

  retrySftpAcquisitionLog(id: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${id}/retry`, {})
      .pipe(
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

  cancelSftpAcquisitionLog(id: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${id}/cancel`, {})
      .pipe(
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

  resetSftpAcquisitionLog(externalId: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${externalId}/reset`, {})
      .pipe(
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      );
  }
}
