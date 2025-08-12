import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable } from 'rxjs';
import { AppConfigService } from 'src/app/services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { IPagedAcquisitionLogSummary } from './models/acquisition-log-summary';
import { AcquisitionLog } from './models/acquisition-log';
import { IDataAcquisitionLogStatistics } from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';

@Injectable({
  providedIn: 'root'
})
export class AcquisitionLogService {

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseUrl = `${this.appConfigService.config?.baseApiUrl}/data/acquisition-logs`;

  getAcquisitionLogs(
    patientId: string | null,
    facilityId: string | null,
    reportId: string | null,
    resourceType: string | null,
    resourceId: string | null,
    queryType: string | null,
    queryPhase: string | null,
    status: string | null,
    priority: string | null,
    sortBy: string | null,
    sortOrder: 'ascending' | 'descending' | null,
    pageNumber: number,
    pageSize: number,
    showLoadingIndicator: boolean = true) : Observable<IPagedAcquisitionLogSummary> {

    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let params: HttpParams = new HttpParams();
    params = params.set('pageNumber', pageNumber.toString());
    params = params.set('pageSize', pageSize.toString());

    if(sortBy) {
        params = params.set('sortBy', sortBy);
    }
    if(sortOrder) {
        params = params.set('sortOrder', sortOrder);
    }

    //add filters to query string
    if(patientId) {
       params = params.set('patientId', patientId);
    }
    if(facilityId) {
        params = params.set('facilityId', facilityId);
    }
    if(reportId) {
        params = params.set('reportId', reportId);
    }
    // if(resourceType) {
    //     params = params.set('resourceType', resourceType);
    // }
    if(resourceId) {
         params = params.set('resourceId', resourceId);
    }
    if(queryType) {
        params = params.set('queryType', queryType);
    }
    if(queryPhase) {
        params = params.set('queryPhase', queryPhase);
    }
    if(status) {
        params = params.set('status', status);
    }
    if(priority) {
        params = params.set('priority', priority);
    }  

    if(showLoadingIndicator)
    {
      return this.http.get<IPagedAcquisitionLogSummary>(`${this.baseUrl}`, { params: params })
      .pipe(
        map((response: IPagedAcquisitionLogSummary) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;          
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
            var err = this.errorHandler.handleError(error);
            return err;
        })
      )
    }
    else
    {
      return this.http.get<IPagedAcquisitionLogSummary>(`${this.baseUrl}`, { params: params, headers: headers })
      .pipe(
        map((response: IPagedAcquisitionLogSummary) => {
          //revert back to zero based paging
          response.metadata.pageNumber--; 
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
            var err = this.errorHandler.handleError(error);
            return err;
        })
      )
    }
  }

  getAcquisitionLog(id: string) : Observable<AcquisitionLog> {    


    return this.http.get<AcquisitionLog>(`${this.baseUrl}/${id}`)
    .pipe(
      map((response: AcquisitionLog) => {
        return response;
      }),
      catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
      })
    )
  }

  executeAcquisitionLog(id: string) : Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${id}/process`, id)
    .pipe(
      map((response: any) => {
        return response;
      }),
      catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
      })
    )
  }

  getAcquisitionLogStatistics(reportId: string): Observable<IDataAcquisitionLogStatistics> {
    return this.http.get<IDataAcquisitionLogStatistics>(`${this.baseUrl}/report/${reportId}/statistics`)
      .pipe(
        map((response: IDataAcquisitionLogStatistics) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getResourceTypes(): Observable<string[]> {

    //temporary test data
    let types = ['Patient', 'Encounter', 'Location', 'Observation', 'MedicationRequest', 'Procedure'];
    return new Observable<string[]>(observer => {

      observer.next(types);
      observer.complete();
    });

    return this.http.get<string[]>(`${this.baseUrl}/acquisition-logs/resource-types`)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }
}
