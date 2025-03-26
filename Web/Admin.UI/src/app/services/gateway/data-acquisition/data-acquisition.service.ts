import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { Observable, tap, map, catchError } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';
import { IDataAcquisitionQueryConfigModel } from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface';
import { IDataAcquisitionFhirListConfigModel } from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import { IDataAcquisitionAuthenticationConfigModel } from '../../../interfaces/data-acquisition/data-acquisition-auth-config-model.interface';
import { AppConfigService } from '../../app-config.service';
import {IQueryPlanModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";

@Injectable({
  providedIn: 'root'
})
export class DataAcquisitionService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  getFhirQueryConfiguration(facilityId: string): Observable<IDataAcquisitionQueryConfigModel> {
    return this.http.get<IDataAcquisitionQueryConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryConfiguration`)
      .pipe(
        tap(_ => console.log(`Fetched FHIR query configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  createFhirQueryConfiguration(facilityId: string, fhirQueryConfig: IDataAcquisitionQueryConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryConfiguration`, fhirQueryConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateFhirQueryConfiguration(facilityId: string, fhirQueryConfig: IDataAcquisitionQueryConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryConfiguration`, fhirQueryConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteFhirQueryConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryConfiguration`)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getFhirListConfiguration(facilityId: string): Observable<IDataAcquisitionFhirListConfigModel> {
    return this.http.get<IDataAcquisitionFhirListConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryList`)
      .pipe(
        tap(_ => console.log(`Fetched FHIR list configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getQueryPlanConfiguration(facilityId: string, type: string): Observable<IQueryPlanModel> {
    return this.http.get<IQueryPlanModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan?type=${type}`)
      .pipe(
        tap(_ => console.log(`Fetched Query Plan configuration.`)),
        map((response: IQueryPlanModel) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  createQueryPlanConfiguration(facilityId: string, queryPlan: IQueryPlanModel): Observable<IQueryPlanModel> {
    return this.http.post<IQueryPlanModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan`, queryPlan)
      .pipe(
        tap(_ => console.log(`Post Query Plan configuration.`)),
        map((response) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateQueryPlanConfiguration(facilityId: string, queryPlan: IQueryPlanModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan`, queryPlan)
      .pipe(
        tap(_ => console.log(`Update Query Plan configuration.`)),
        map((response) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  createFhirListConfiguration(facilityId: string, fhirListConfig: IDataAcquisitionFhirListConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryList`, fhirListConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  //NOTE: currently no PUT endpoint for fhir list. Commenting this out for now.
  updateFhirListConfiguration(facilityId: string, fhirListConfig: IDataAcquisitionFhirListConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryList`, fhirListConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteFhirListConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryList`)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getAuthenticationConfig(facilityId: string, queryConfigType: string) {
    return this.http.get<IDataAcquisitionAuthenticationConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`)
    .pipe(
        tap(_ => console.log(`Fetched authentication configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  createAuthenticationConfig(facilityId: string, queryConfigType: string, authenticationConfig: IDataAcquisitionAuthenticationConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`, authenticationConfig)
      .pipe(
        tap(_ => console.log(`Request for authentication configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateAuthenticationConfig(facilityId: string, queryConfigType: string, authenticationConfig: IDataAcquisitionAuthenticationConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`, authenticationConfig)
      .pipe(
        tap(_ => console.log(`Request for authentication configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
