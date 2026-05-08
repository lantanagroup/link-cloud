import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable} from 'rxjs';
import { catchError, map, retry, tap } from 'rxjs/operators';
import { IEntityCreatedResponse } from '../../interfaces/entity-created-response.model';
import { ErrorHandlingService } from '../error-handling.service';
import { IPatientEvent } from '../../interfaces/testing/patient-event.interface';
import { IDataAcquisitionRequested, IScheduledReport } from '../../interfaces/testing/data-acquisition-requested.interface';
import { IReportScheduled } from '../../interfaces/testing/report-scheduled.interface';
import { AppConfigService } from '../app-config.service';
import {IDataPatientAcquiredRequested} from "../../interfaces/testing/patient-acquired.interface";
import {IPatientListAcquired} from "../../interfaces/testing/patient-list-acquired.interface";

@Injectable({
  providedIn: 'root'
})
export class TestService {


  constructor(private http: HttpClient,
              private errorHandler: ErrorHandlingService,
              public appConfigService: AppConfigService) {

  }

  generateReportScheduledEvent(facilityId: string, reportTypes: string[], frequency:string, startDate: Date, delay: string, reportTrackingId: string ): Observable<IEntityCreatedResponse> {
    let event: IReportScheduled = {
      facilityId: facilityId,
      frequency: frequency,
      reportTypes: reportTypes,
      startDate: startDate,
      delay: delay,
      reportTrackingId : reportTrackingId
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/report-scheduled`, event)
      .pipe(
        tap(_ => console.log(`Request for a new report scheduled event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  generatePatientEvent(facilityId: string, patientId: string, eventType: string): Observable<IEntityCreatedResponse> {

    let event: IPatientEvent = {
      key: facilityId,
      patientId: patientId,
      eventType: eventType
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/patient-event`, event)
      .pipe(
        tap(_ => console.log(`Request for a new patient event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
    )
  }

  startConsumers(correlationId:string): Observable<IEntityCreatedResponse> {

    let event = {correlationId: correlationId};

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/start-consumers`, event)
      .pipe(
        tap(_ => console.log(`Request for creating consumers.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  readConsumers(correlationId:string): Observable<{ [key: string]: string }> {

    let event = {correlationId: correlationId};

    return this.http.post<{ [key: string]: string }>(`${this.appConfigService.config?.baseApiUrl}/integration/read-consumers`, event)
      .pipe(
        tap(_ => console.log(`Request for reading consumers.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  stopConsumers(correlationId:string): Observable<any> {

    let event = {correlationId: correlationId};

    return this.http.post<{ [key: string]: string }>(`${this.appConfigService.config?.baseApiUrl}/integration/stop-consumers`, event)
      .pipe(
        tap(_ => console.log(`Request for stopping consumers.`)),
        map((response: { [key: string]: string }) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  generateDataAcquisitionRequestedEvent(facilityId: string, patientId: string, reports: IScheduledReport[]): Observable<IEntityCreatedResponse> {

    let event: IDataAcquisitionRequested = {
      key: facilityId,
      patientId: patientId,
      reports: reports
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/data-acquisition-requested`, event)
      .pipe(
        tap(_ => console.log(`Request for a new data acquisition requested event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  generatePatientAcquiredEvent(facilityId: string, patientIds: string[], reportTrackingId: string): Observable<IEntityCreatedResponse> {

    let event: IDataPatientAcquiredRequested = {
      facilityId: facilityId,
      patientIds: patientIds,
      reportTrackingId: reportTrackingId
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/patient-acquired`, event)
      .pipe(
        tap(_ => console.log(`Request for a new patient acquisition requested event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  generatePatientListAcquiredEvent(facilityId: string, patientList: {
    listType: "Admit" | "Discharge";
    timeFrame: "LessThan24Hours" | "Between24To48Hours" | "MoreThan48Hours";
    patientIds: string[]
  }[], reportTrackingId: string): Observable<IEntityCreatedResponse> {

    let event: IPatientListAcquired  = {
      facilityId: facilityId,
      patientLists: patientList,
      reportTrackingId: reportTrackingId
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/patient-list-acquired`, event)
      .pipe(
        tap(_ => console.log(`Request for a new patient list acquisition requested event was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }


  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
