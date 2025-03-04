import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class MeasureDefinitionService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  createMeasureDefinitionConfiguration(measureConfiguration: IMeasureDefinitionConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/measure-definition/`, measureConfiguration)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateMeasureDefinitionConfiguration(measureConfiguration: IMeasureDefinitionConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/measure-definition/${measureConfiguration.bundleId}`, measureConfiguration.bundle)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getMeasureDefinitionConfiguration(bundleId: string): Observable<IMeasureDefinitionConfigModel> {
    return this.http.get<IMeasureDefinitionConfigModel>(`${this.appConfigService.config?.baseApiUrl}/measure-definition/${bundleId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteMeasureDefinitionConfiguration(bundleId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/measure-definition/${bundleId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

   getMeasureDefinitionConfigurations(): Observable<IMeasureDefinitionConfigModel[]> {
     return this.http.get<IMeasureDefinitionConfigModel[]>(`${this.appConfigService.config?.baseApiUrl}/measure-definition/measures`)
       .pipe(
         tap(_ => console.log(`Fetched measure definitions.`)),
         map((response: IMeasureDefinitionConfigModel[]) => {
           return response;
         }),
         catchError((error) => this.errorHandler.handleError(error))
       )
   }

}
