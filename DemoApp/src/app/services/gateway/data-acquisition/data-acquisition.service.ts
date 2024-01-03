import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { ErrorHandlingService } from '../../error-handling.service';
import { IDataAcquisitionFacilityModel, ITenantDataAcquisitionConfigModel } from 'src/app/interfaces/data-acquisition/data-acquisition-config-model.interface';
import { Observable, tap, map, catchError } from 'rxjs';
import { ICensusConfiguration } from 'src/app/interfaces/census/census-config-model.interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IEntityDeletedResponse } from 'src/app/interfaces/entity-deleted-response.interface';

@Injectable({
  providedIn: 'root'
})
export class DataAcquisitionService {
  private baseApiUrl = `${environment.baseApiUrl}/api`;

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService) { }

  createConfiguration(tenantId: string, facilities: IDataAcquisitionFacilityModel[]): Observable<IEntityCreatedResponse> {
    let config: ITenantDataAcquisitionConfigModel = {
      id: '',
      tenantId: tenantId,
      facilities: facilities
    };

    return this.http.post<IEntityCreatedResponse>(`${this.baseApiUrl}/data/config`, config)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateConfiguration(id: string, tenantId: string, facilityId: string, facility: IDataAcquisitionFacilityModel): Observable<IEntityCreatedResponse> {
    let config: ITenantDataAcquisitionConfigModel = {
      id: id,
      tenantId: tenantId,      
      facilities: [facility]
    };

    return this.http.put<IEntityCreatedResponse>(`${this.baseApiUrl}/data/config/${facilityId}`, config)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getConfiguration(facilityId: string): Observable<ICensusConfiguration> {
    return this.http.get<ICensusConfiguration>(`${this.baseApiUrl}/data/config/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.baseApiUrl}/data/config/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }
}
