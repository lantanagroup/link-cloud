import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map, retry, tap } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { PagedNotificationModel } from '../../models/notification/paged-notification-model.model';
import { INotificationMessage } from '../../interfaces/notification/notification-message.interface';
import { IEntityCreatedResponse } from '../../interfaces/entity-created-response.model';
import { IFacilityChannel, INotificationConfiguration } from '../../interfaces/notification/notification-configuration-model.interface';
import { PagedNotificationConfigurationModel } from '../../models/notification/paged-notification-configuration-model.model';
import { IEntityDeletedResponse } from '../../interfaces/entity-deleted-response.interface';
import { AppConfigService } from '../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  constructor(private http: HttpClient, public appConfigService: AppConfigService) { }

  list(searchText: string, filterFacilityBy: string, filterNotificationTypeBy: string, createdOnStart: Date | null, createdOnEnd: Date | null, sentOnStart: Date | null, sentOnEnd: Date | null, sortBy: string, pageSize: number, pageNumber: number): Observable<PagedNotificationModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    return this.http.get<PagedNotificationModel>(`${this.appConfigService.config?.baseApiUrl}/notification?searchText=${searchText}&filterFacilityBy=${filterFacilityBy}&filterNotificationTypeBy=${filterNotificationTypeBy}&sortBy=${sortBy}&pageSize=${pageSize}&pageNumber=${pageNumber}`)
      .pipe(
        tap(_ => console.log(`Fetched notifications.`)),
        map((response: PagedNotificationModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;
          return response;
        }),
        catchError(this.handleError)
      )
  }

  create(notificationType: string, facilityId: string, subject: string, body: string, recipients: string[], bcc: string[]): Observable<IEntityCreatedResponse> {
    let notification: INotificationMessage = {
        notificationType: notificationType,
        facilityId: facilityId,
        subject: subject,
        body: body,
        recipients: recipients,
        bcc: bcc,
        correlationId: null
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/notification`, notification)
      .pipe(
        tap(_ => console.log(`Request for notification creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )  
  }

  getFacilityConfiguration(facilityId: string): Observable<INotificationConfiguration> {
    return this.http.get<INotificationConfiguration>(`${this.appConfigService.config?.baseApiUrl}/notification/configuration/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched notification configuration`)),
        map((response: INotificationConfiguration) => {
          return response;
        }),
        catchError(this.handleError)        
      )
  }

  listFacilityConfiguration(searchText: string, filterFacilityBy: string, sortBy: string, pageSize: number, pageNumber: number): Observable<PagedNotificationConfigurationModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    return this.http.get<PagedNotificationConfigurationModel>(`${this.appConfigService.config?.baseApiUrl}/notification/configuration?searchText=${searchText}&filterFacilityBy=${filterFacilityBy}&sortBy=${sortBy}&pageSize=${pageSize}&pageNumber=${pageNumber}`)
      .pipe(
        tap(_ => console.log(`Fetched notification configurations.`)),
        map((response: PagedNotificationConfigurationModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;
          return response;
        }),
        catchError(this.handleError)
      )
  }

  createFacilityConfiguration(facilityId: string, recipients: string[], channels: IFacilityChannel[]): Observable<IEntityCreatedResponse> {
    let notificationConfiguration: INotificationConfiguration = {
      id: '00000000-0000-0000-0000-000000000000',
      facilityId: facilityId,
      emailAddresses: recipients,
      channels: channels
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/notification/configuration`, notificationConfiguration)
      .pipe(
        tap(_ => console.log(`Request for notification configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )  
  }

  updateFacilityConfiguration(id: string, facilityId: string, recipients: string[], channels: IFacilityChannel[]): Observable<IEntityCreatedResponse> {
    let notificationConfiguration: INotificationConfiguration = {
      id: id,
      facilityId: facilityId,
      emailAddresses: recipients,
      channels: channels
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/notification/configuration`, notificationConfiguration)
      .pipe(
        tap(_ => console.log(`Request for notification configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )
  }

  deleteFacilityConfiguration(id: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/notification/configuration/${id}`)
      .pipe(
        tap(_ => console.log(`Request for notification configuration deletion was sent.`)),
        map((response: IEntityDeletedResponse) => {
          return response;
        }),
        catchError(this.handleError)
      )
  }

  private handleError(err: HttpErrorResponse) {
    let errorMessage = '';

    if (err.error instanceof ErrorEvent) {
      errorMessage = `An error occured: ${err.error.message}`;
    }
    else {
      errorMessage = `Server returned code: ${err.status}, error message is: ${err.message}`;
    }

    console.error(errorMessage);
    return throwError(() => errorMessage);

  }

}
