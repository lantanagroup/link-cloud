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
import {MatSnackBar} from '@angular/material/snack-bar';

@Injectable({
  providedIn: 'root'
})
export class TestService {


  constructor(private http: HttpClient,
              private snackBar: MatSnackBar,
              private errorHandler: ErrorHandlingService,
              public appConfigService: AppConfigService) {
    console.log(this.snackBar);
  }

  generateReportScheduledEvent(facilityId: string, reportTypes: string[], frequency:string, startDate: Date, delay: string): Observable<IEntityCreatedResponse> {
    let event: IReportScheduled = {
      facilityId: facilityId,
      frequency: frequency,
      reportTypes: reportTypes,
      startDate: startDate,
      delay: delay
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

  startConsumers(facilityId:string): Observable<IEntityCreatedResponse> {

    let event = {facilityId: facilityId};

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/integration/start-consumers`, event)
      .pipe(
        tap(_ => console.log(`Request for creating consumers.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  readConsumers(facilityId:string): Observable<{ [key: string]: string }> {

    let event = {facilityId: facilityId};

    return this.http.post<{ [key: string]: string }>(`${this.appConfigService.config?.baseApiUrl}/integration/read-consumers`, event)
      .pipe(
        tap(_ => console.log(`Request for reading consumers.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  stopConsumers(facilityId:string): Observable<any> {

    let event = {facilityId: facilityId};

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

  generatePatientAcquiredEvent(facilityId: string, patientIds: string[]): Observable<IEntityCreatedResponse> {

    let event: IDataPatientAcquiredRequested = {
      facilityId: facilityId,
      patientIds: patientIds,
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

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
