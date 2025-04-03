import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { ICensusConfiguration } from 'src/app/interfaces/census/census-config-model.interface';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class CensusService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  createConfiguration(facilityId: string, scheduledTrigger: string): Observable<IEntityCreatedResponse> {
    let census: ICensusConfiguration = {
      facilityId: facilityId,
      scheduledTrigger: scheduledTrigger
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/census/config`, census)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateConfiguration(facilityId: string, scheduledTrigger: string): Observable<IEntityCreatedResponse> {
    let census: ICensusConfiguration = {
      facilityId: facilityId,
      scheduledTrigger: scheduledTrigger
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/census/config/${facilityId}`, census)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getConfiguration(facilityId: string): Observable<ICensusConfiguration> {
    return this.http.get<ICensusConfiguration>(`${this.appConfigService.config?.baseApiUrl}/census/config/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error,false);
        })
      )
  }

  deleteConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/census/config/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
