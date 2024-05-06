import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ErrorHandlingService } from '../../error-handling.service';
import { IReportConfigModel } from 'src/app/interfaces/report/report-config-model.interface';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }


  createReportConfiguration(facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: "",
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Create`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateReportConfiguration(reportConfigId: string, facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: reportConfigId,
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Update?id=${reportConfigId}`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getReportConfiguration(reportConfigId: string): Observable<IReportConfigModel> {
    return this.http.get<IReportConfigModel>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Get?id=/${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteReportConfiguration(reportConfigId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Delete/?id=${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getReportConfigurations(facilityId: string): Observable<IReportConfigModel[]> {
    return this.http.get<IReportConfigModel[]>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched reports.`)),
        map((response: IReportConfigModel[]) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
