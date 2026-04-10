import {HttpClient, HttpErrorResponse, HttpHeaders, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {catchError, map, Observable} from 'rxjs';
import {AppConfigService} from 'src/app/services/app-config.service';
import {ErrorHandlingService} from 'src/app/services/error-handling.service';
import {IPagedAcquisitionLogSummary} from './models/acquisition-log-summary';
import {AcquisitionLog} from './models/acquisition-log';
import {
  IDataAcquisitionLogStatistics
} from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';
import {
  IDataAcquisitionLogStatusStatistics
} from 'src/app/interfaces/data-acquisition/data-acquisition-log-status-statistics.interface';

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
    status: string[] | string | null,
    priority: string | null,
    sortBy: string | null,
    sortOrder: 'ascending' | 'descending' | null,
    pageNumber: number,
    pageSize: number,
    showLoadingIndicator: boolean = true,
    includeDeleted: boolean = false,
    createdBefore: string | null = null) : Observable<IPagedAcquisitionLogSummary> {

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
    if(resourceType) {
        params = params.set('resourceType', resourceType);
    }
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
        if (Array.isArray(status)) {
            status.forEach(s => {
                params = params.append('statuses', s);
            });
        } else {
            params = params.append('statuses', status);
        }
    }
    if(priority) {
        params = params.set('priority', priority);
    }
    if(includeDeleted) {
        params = params.set('includeDeleted', 'true');
    }
    if(createdBefore) {
        params = params.set('createdBefore', createdBefore);
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
    return this.http.post<any>(`${this.baseUrl}/${id}/process`, Number(id))
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

  cancelBulkAcquisitionLogs(ids: string[], minAgeHours: number = 24) : Observable<{ requested: number; cancelled: number; ineligible: number }> {
    const params = new HttpParams().set('minAgeHours', minAgeHours.toString());
    const numericIds = ids.map(id => Number(id));
    return this.http.post<{ requested: number; cancelled: number; ineligible: number }>(`${this.baseUrl}/cancel-bulk`, numericIds, { params })
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

  cancelAcquisitionLogsByFilter(
    patientId: string | null,
    facilityId: string | null,
    reportId: string | null,
    resourceType: string | null,
    resourceId: string | null,
    queryType: string | null,
    queryPhase: string | null,
    status: string[] | string | null,
    priority: string | null,
    createdBefore: string | null = null,
    minAgeHours: number = 24) : Observable<{ requested: number; cancelled: number; ineligible: number }> {

    let body: any = {
      patientId,
      facilityId,
      reportId,
      resourceType,
      resourceId,
      queryType,
      queryPhase,
      priority,
      createdBefore
    };

    if (status) {
      body.statuses = Array.isArray(status) ? status : [status];
    }

    const params = new HttpParams().set('minAgeHours', minAgeHours.toString());
    return this.http.post<{ requested: number; cancelled: number; ineligible: number }>(`${this.baseUrl}/cancel-by-filter`, body, { params })
    .pipe(
      catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
      })
    )
  }

  bulkExecuteAcquisitionLogs(ids: string[]) : Observable<any> {
    const numericIds = ids.map(id => Number(id));
    return this.http.post<any>(`${this.baseUrl}/process-bulk`, numericIds)
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

  bulkExecuteAcquisitionLogsByFilter(
    patientId: string | null,
    facilityId: string | null,
    reportId: string | null,
    resourceType: string | null,
    resourceId: string | null,
    queryType: string | null,
    queryPhase: string | null,
    status: string[] | string | null,
    priority: string | null,
    createdBefore: string | null = null) : Observable<any> {

    let body: any = {
      patientId,
      facilityId,
      reportId,
      resourceType,
      resourceId,
      queryType,
      queryPhase,
      priority,
      createdBefore
    };

    if (status) {
      body.statuses = Array.isArray(status) ? status : [status];
    }

    return this.http.post<any>(`${this.baseUrl}/process-by-filter`, body)
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
    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });

    return this.http.get<IDataAcquisitionLogStatistics>(`${this.baseUrl}/report/${reportId}/statistics`, { headers: headers })
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

  getAcquisitionLogStatusStatistics(reportId: string, patientId?: string | null): Observable<IDataAcquisitionLogStatusStatistics> {
    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });
    let params = new HttpParams();

    if (patientId) {
      params = params.set('patientId', patientId);
    }

    return this.http.get<IDataAcquisitionLogStatusStatistics>(`${this.baseUrl}/report/${reportId}/status-counts`, { headers: headers, params: params })
      .pipe(
        map((response: IDataAcquisitionLogStatusStatistics) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  softDeleteByFacility(facilityId: string): Observable<number> {
    return this.http.delete<number>(`${this.baseUrl}/facility/${encodeURIComponent(facilityId)}`)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

  restoreByFacility(facilityId: string): Observable<number> {
    return this.http.patch<number>(`${this.baseUrl}/facility/${encodeURIComponent(facilityId)}/restore`, {})
      .pipe(
        catchError((error: HttpErrorResponse) => {
          return this.errorHandler.handleError(error);
        })
      );
  }

}
