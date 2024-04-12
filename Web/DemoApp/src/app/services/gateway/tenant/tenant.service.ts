import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { ErrorHandlingService } from '../../error-handling.service';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { IFacilityConfigModel, IScheduledTaskModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  createFacility(facilityId: string, facilityName: string, scheduledTasks: IScheduledTaskModel[]): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      facilityId: facilityId,
      facilityName: facilityName,
      scheduledTasks: scheduledTasks
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/tenant/facility`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateFacility(id: string, facilityId: string, facilityName: string, scheduledTasks: IScheduledTaskModel[]): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      id: id,
      facilityId: facilityId,
      facilityName: facilityName,
      scheduledTasks: scheduledTasks
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/tenant/facility/${id}`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getFacilityConfiguration(facilityId: string): Observable<IFacilityConfigModel> {
    return this.http.get<IFacilityConfigModel>(`${this.appConfigService.config?.baseApiUrl}/tenant/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched facility configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  listFacilities(facilityId: string, facilityName: string): Observable<IFacilityConfigModel[]> {
    return this.http.get<IFacilityConfigModel[]>(`${this.appConfigService.config?.baseApiUrl}/tenant/facility?facilityId=${facilityId}&facilityName=${facilityName}`)
      .pipe(
        tap(_ => console.log(`Fetched facilities.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }
  
}
