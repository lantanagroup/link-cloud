import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ErrorHandlingService } from '../../error-handling.service';
import { IReportConfigModel } from 'src/app/interfaces/report/report-config-model.interface';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private baseApiUrl = `${environment.baseApiUrl}/api`;

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService) { }

  createConfiguration(facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: "",
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.post<IEntityCreatedResponse>(`${this.baseApiUrl}/report/config`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateConfiguration(reportConfigId: string, facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: reportConfigId,
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.put<IEntityCreatedResponse>(`${this.baseApiUrl}/report/config/${reportConfigId}`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getConfiguration(reportConfigId: string): Observable<IReportConfigModel> {
    return this.http.get<IReportConfigModel>(`${this.baseApiUrl}/report/config/${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteConfiguration(reportConfigId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.baseApiUrl}/report/config/${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getReports(facilityId: string): Observable<IReportConfigModel[]> {
    return this.http.get<IReportConfigModel[]>(`${this.baseApiUrl}/report/config/reports/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched reports.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
