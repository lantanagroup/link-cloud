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

  updateMeasureDefinitionConfiguration(measureConfiguration: IMeasureDefinitionConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/measureeval/measure-definition`, measureConfiguration.bundle, { headers: { 'Content-Type': 'application/fhir+json' } })
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

   getMeasureDefinitionConfigurations(): Observable<IMeasureDefinitionConfigModel[]> {
     return this.http.get<IMeasureDefinitionConfigModel[]>(`${this.appConfigService.config?.baseApiUrl}/measureeval/measure-definition`)
       .pipe(
         tap(_ => console.log(`Fetched measure definitions.`)),
         map((response: IMeasureDefinitionConfigModel[]) => {
           return response;
         }),
         catchError((error) => this.errorHandler.handleError(error))
       )
   }

}
