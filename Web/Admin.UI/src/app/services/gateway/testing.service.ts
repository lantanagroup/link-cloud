import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map, retry, tap } from 'rxjs/operators';
import { PagedAuditModel } from '../../models/audit/paged-audit-model.model';
import { environment } from '../../../environments/environment';
import { IEntityCreatedResponse } from '../../interfaces/entity-created-response.model';
import { ErrorHandlingService } from '../error-handling.service';
import { IPatientEvent } from '../../interfaces/testing/patient-event.interface';
import { IDataAcquisitionRequested, IScheduledReport } from '../../interfaces/testing/data-acquisition-requested.interface';
import { IReportScheduled } from '../../interfaces/testing/report-scheduled.interface';
import { AppConfigService } from '../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class TestService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  generateReportScheduledEvent(facilityId: string, reportType: string, startDate: Date, endDate: Date): Observable<IEntityCreatedResponse> {
    let event: IReportScheduled = {
      facilityId: facilityId,
      reportType: reportType,
      startDate: startDate,
      endDate: endDate
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/testing/report-scheduled`, event)
      .pipe(
        tap(_ => console.log(`Request for a new report scheduled event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )
  }

  generatePatientEvent(facilityId: string, patientId: string, eventType: string): Observable<IEntityCreatedResponse> {

    let event: IPatientEvent = {
      key: facilityId,
      patientId: patientId,
      eventType: eventType
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/testing/patient-event`, event)
      .pipe(
        tap(_ => console.log(`Request for a new patient event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )
  }

  generateDataAcquisitionRequestedEvent(facilityId: string, patientId: string, reports: IScheduledReport[]): Observable<IEntityCreatedResponse> {

    let event: IDataAcquisitionRequested = {
      key: facilityId,
      patientId: patientId,
      reports: reports
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/testing/data-acquisition-requested-event`, event)
      .pipe(
        tap(_ => console.log(`Request for a new data acquisition requested event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
