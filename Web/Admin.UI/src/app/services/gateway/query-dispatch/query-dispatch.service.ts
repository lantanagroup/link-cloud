import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';
import { AppConfigService } from '../../app-config.service';
import {IQueryDispatchConfiguration} from "../../../interfaces/query-dispatch/query-dispatch-config-model.interface";


@Injectable({
  providedIn: 'root'
})
export class QueryDispatchService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  createConfiguration(facilityId: string, dispatchSchedules: any): Observable<IEntityCreatedResponse> {
    let queryDispatchConfig: IQueryDispatchConfiguration = {
      facilityId: facilityId,
      dispatchSchedules: dispatchSchedules
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/querydispatch/configuration`, queryDispatchConfig)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateConfiguration(facilityId: string, dispatchSchedules: any ): Observable<IEntityCreatedResponse> {
    let queryDispatchConfig: IQueryDispatchConfiguration = {
      facilityId: facilityId,
      dispatchSchedules: dispatchSchedules
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/querydispatch/configuration/facility/${facilityId}`, queryDispatchConfig)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getConfiguration(facilityId: string): Observable<IQueryDispatchConfiguration> {
    return this.http.get<IQueryDispatchConfiguration>(`${this.appConfigService.config?.baseApiUrl}/querydispatch/configuration/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error,false);
        })
      )
  }

  deleteConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/querydispatch/configuration/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
