import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  IFacilityConfigModel,
  IScheduledReportModel,
  PagedFacilityConfigModel
} from 'src/app/interfaces/tenant/facility-config-model.interface';
import { Observable, catchError, map, tap, of } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }


  createFacility(facilityId: string, facilityName: string, timeZone: string, scheduledReports: IScheduledReportModel): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      facilityId: facilityId,
      facilityName: facilityName,
      timeZone: timeZone,
      scheduledReports: scheduledReports
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateFacility(id: string, facilityId: string, facilityName: string, timeZone: string, scheduledReports: IScheduledReportModel): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      id: id,
      facilityId: facilityId,
      facilityName: facilityName,
      timeZone: timeZone,
      scheduledReports: scheduledReports
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility/${id}`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getFacilityConfiguration(facilityId: string): Observable<IFacilityConfigModel> {
    return this.http.get<IFacilityConfigModel>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched facility configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  checkFacility(facilityId: string): Observable<boolean> {
    // Replace this URL with your API endpoint
    return this.http.get<boolean>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`)
      .pipe(
        catchError(() => of(false)) // Return false if there's an error
    );
  }


  listFacilities(facilityId: string, facilityName: string): Observable<PagedFacilityConfigModel> {
    return this.http.get<PagedFacilityConfigModel>(`${this.appConfigService.config?.baseApiUrl}/facility?facilityId=${facilityId}&facilityName=${facilityName}`)
      .pipe(
        tap(_ => console.log(`Fetched facilities.`)),
        map((response: PagedFacilityConfigModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;
          return response;
        }),
        catchError(this.handleError)
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
