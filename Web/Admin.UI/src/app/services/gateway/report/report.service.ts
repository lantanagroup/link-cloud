import {HttpClient, HttpHeaders, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {IReportConfigModel} from 'src/app/interfaces/report/report-config-model.interface';
import {IPagedReportSchedule, IReportSchedule} from 'src/app/interfaces/report/report-schedule.interface';
import {catchError, map, Observable, tap} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {IEntityDeletedResponse} from 'src/app/interfaces/entity-deleted-response.interface';
import {AppConfigService} from '../../app-config.service';
import {
  IPagedReportEntry,
  IReportEntrySummary,
  ReportingStatus,
  SubmissionStatus
} from 'src/app/interfaces/report/report-entry.interface';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  createReportConfiguration(facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: "",
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Create`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateReportConfiguration(reportConfigId: string, facilityId: string, reportType: string, bundlingType: string): Observable<IEntityCreatedResponse> {
    let report: IReportConfigModel = {
      id: reportConfigId,
      facilityId: facilityId,
      reportType: reportType,
      bundlingType: bundlingType
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Update?id=${reportConfigId}`, report)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getReportConfiguration(reportConfigId: string): Observable<IReportConfigModel> {
    return this.http.get<IReportConfigModel>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Get?id=/${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteReportConfiguration(reportConfigId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/Delete/?id=${reportConfigId}`)
      .pipe(
        tap(_ => console.log(`Request for configuration deletion was sent.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getReportConfigurations(facilityId: string): Observable<IReportConfigModel[]> {
    return this.http.get<IReportConfigModel[]>(`${this.appConfigService.config?.baseApiUrl}/reportconfig/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched reports.`)),
        map((response: IReportConfigModel[]) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteReports(facilityId: string): Observable<void> {
    return this.http.patch<void>(
      `${this.appConfigService.config?.baseApiUrl}/schedules/facility/${facilityId}/status?deleted=true`,
      {}
    ).pipe(
      tap(_ => console.log(`Request to disable report configuration was sent.`)),
      catchError(error => this.errorHandler.handleError(error))
    );
  }

  restoreReports(facilityId: string): Observable<void> {
    return this.http.patch<void>(
      `${this.appConfigService.config?.baseApiUrl}/schedules/facility/${facilityId}/status?deleted=false`,
      {}
    ).pipe(
      tap(_ => console.log(`Request to restore report configuration was sent.`)),
      catchError(error => this.errorHandler.handleError(error))
    );
  }

  searchReportSchedules(
    facilityId?: string,
    frequency?: string,
    reportType?: string,
    reportStartDate?: Date,
    reportEndDate?: Date,
    status?: string,
    endOfReportPeriodJobHasRun?: boolean,
    includeDeleted?: boolean,
    sortBy?: string,
    sortOrder?: number,
    pageSize: number = 10,
    pageNumber: number = 1,
    createDate?: Date
  ): Observable<IPagedReportSchedule> {
    let params = new HttpParams()
      .set('pageSize', pageSize.toString())
      .set('pageNumber', pageNumber.toString());

    if (facilityId) {
      params = params.set('facilityId', facilityId);
    }
    if (frequency) {
      params = params.set('frequency', frequency);
    }
    if (reportType) {
      params = params.set('reportType', reportType);
    }
    if (reportStartDate) {
      params = params.set('reportStartDate', reportStartDate.toISOString());
    }
    if (reportEndDate) {
      params = params.set('reportEndDate', reportEndDate.toISOString());
    }
    if (status) {
      params = params.set('status', status);
    }
    if (endOfReportPeriodJobHasRun !== undefined) {
      params = params.set('endOfReportPeriodJobHasRun', endOfReportPeriodJobHasRun.toString());
    }
    if (includeDeleted !== undefined) {
      params = params.set('includeDeleted', includeDeleted.toString());
    }
    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    if (sortOrder !== undefined) {
      params = params.set('sortOrder', sortOrder.toString());
    }
    if (createDate) {
      params = params.set('createDate', createDate.toISOString());
    }

    return this.http.get<IPagedReportSchedule>(`${this.appConfigService.config?.baseApiUrl}/aggregate/reports/summaries`, { params })
      .pipe(
        tap(_ => console.log('Fetched report schedules.')),
        catchError((error) => this.errorHandler.handleError(error))
      );
  }

  searchReportEntries(
    facilityId?: string,
    patientId?: string,
    reportScheduleId?: string,
    reportingStatuses?: ReportingStatus[],
    submissionStatuses?: SubmissionStatus[],
    submissionStatusIsNull?: boolean,
    reportType?: string,
    sortBy?: string,
    sortOrder?: 'ascending' | 'descending',
    pageSize: number = 10,
    pageNumber: number = 1
  ): Observable<IPagedReportEntry> {
    let params = new HttpParams()
      .set('pageSize', pageSize.toString())
      .set('pageNumber', pageNumber.toString());

    if (facilityId) {
      params = params.set('facilityId', facilityId);
    }
    if (patientId) {
      params = params.set('patientId', patientId);
    }
    if (reportScheduleId) {
      params = params.set('reportScheduleId', reportScheduleId);
    }
    if (reportingStatuses && reportingStatuses.length > 0) {
      reportingStatuses.forEach(status => {
        params = params.append('reportingStatuses', status);
      });
    }
    if (submissionStatuses && submissionStatuses.length > 0) {
      submissionStatuses.forEach(status => {
        params = params.append('submissionStatuses', status);
      });
    }
    if (submissionStatusIsNull) {
      params = params.set('submissionStatusIsNull', 'true');
    }
    if (reportType) {
      params = params.set('reportType', reportType);
    }
    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    if (sortOrder) {
      // Convert to backend enum value (0 = Ascending, 1 = Descending)
      const sortOrderValue = sortOrder === 'ascending' ? '0' : '1';
      params = params.set('sortOrder', sortOrderValue);
    }

    return this.http.get<IPagedReportEntry>(`${this.appConfigService.config?.baseApiUrl}/entries/search`, { params })
      .pipe(
        tap(_ => console.log('Fetched report entries.')),
        catchError((error) => this.errorHandler.handleError(error))
      );
  }

  getReportSchedule(reportScheduleId: string): Observable<IReportSchedule> {
    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });
    return this.http.get<IReportSchedule>(`${this.appConfigService.config?.baseApiUrl}/aggregate/reports/summaries/${reportScheduleId}`, { headers })
      .pipe(
        tap(_ => console.log('Fetched report schedule.')),
        catchError((error) => this.errorHandler.handleError(error))
      );
  }

  getReportEntrySummary(reportScheduleId: string): Observable<IReportEntrySummary> {
    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });
    return this.http.get<IReportEntrySummary>(`${this.appConfigService.config?.baseApiUrl}/entries/schedules/${reportScheduleId}/summary`, { headers })
      .pipe(
        tap(_ => console.log('Fetched report entry summary.')),
        catchError((error) => this.errorHandler.handleError(error))
      );
  }
}
