import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {catchError, map, Observable, tap} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {
  IMeasureDefinitionConfigModel
} from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import {AppConfigService} from '../../app-config.service';
import {IRelatedArtifact} from '../../../interfaces/measure-definition/related-artifact.interface';

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

   getRelatedArtifacts(id: string): Observable<IRelatedArtifact[]> {
     return this.http.get<IRelatedArtifact[]>(`${this.appConfigService.config?.baseApiUrl}/measureeval/measure-definition/${id}/relatedArtifact`)
       .pipe(
         tap(_ => console.log(`Fetched related artifacts for ${id}.`)),
         catchError((error) => this.errorHandler.handleError(error))
       )
   }

}
