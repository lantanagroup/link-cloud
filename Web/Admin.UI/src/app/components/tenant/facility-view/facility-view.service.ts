import { HttpClient, HttpErrorResponse, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { catchError, map, Observable } from "rxjs";
import { AppConfigService } from "src/app/services/app-config.service";
import { ICategoryIssuePage, IReportCategoryCounts, IPagedMeasureReportSummary, IPagedReportListSummary, IPagedResourceSummary, IReportListSummary } from "./report-view.interface";
import { ErrorHandlingService } from "src/app/services/error-handling.service";
import { IApiResponse } from "src/app/interfaces/api-response.interface";
import {IPagedReportSchedule} from "../../../interfaces/report/report-schedule.interface";


@Injectable({
  providedIn: 'root'
})
export class FacilityViewService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  downloadValidationResults(facilityId: string, reportId: string): void {
    // Let the browser stream the attachment directly to disk instead of buffering a multi-GB blob.
    window.location.href = `${this.appConfigService.config?.baseApiUrl}/validation/facilities/${encodeURIComponent(facilityId)}/reports/${encodeURIComponent(reportId)}/results.csv`;
  }

  downloadReport(facilityId: string, reportId: string) {
    window.location.href = `${this.appConfigService.config?.baseApiUrl}/Submission/${facilityId}/${reportId}`;
  }

  getReportSummaryList(facilityId: string, pageNumber: number, pageSize: number, showDeleted: boolean): Observable<IPagedReportSchedule> {
    //javascript based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    return this.http.get<IPagedReportSchedule>(`${this.appConfigService.config?.baseApiUrl}/schedules/search?facilityId=${facilityId}&pageNumber=${pageNumber}&pageSize=${pageSize}&includeDeleted=${showDeleted}`)
      .pipe(
        map((response: IPagedReportSchedule) => {
          //revert back to zero based paging
          if (response) {
            response.metadata.pageNumber--;
          }
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getReportSummary(facilityId: string, reportId: string): Observable<IReportListSummary> {
    return this.http.get<IReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/schedules/${reportId}`)
      .pipe(
        map((response: IReportListSummary) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getMeasureReportSummaryList(facilityId: string, reportId: string,
    patientId: string | null, measureReportId: string | null, measure: string | null,
    reportStatus: string | null, validationStatus: string | null, sortBy: string | null,
    sortOrder: 'ascending' | 'descending' | null, pageNumber: number, pageSize: number): Observable<IPagedMeasureReportSummary> {

    //javascript based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let params: HttpParams = new HttpParams();
    params = params.append('pageNumber', pageNumber.toString());
    params = params.append('pageSize', pageSize.toString());
    params = params.append('reportId', reportId);

    //add filters to query string
    if (patientId) {
      params = params.append('patientId', patientId);
    }
    if (measureReportId) {
      params = params.append('measureReportId', measureReportId);
    }
    if (measure) {
      params = params.append('measure', measure);
    }
    if (reportStatus) {
      params = params.append('reportStatus', reportStatus);
    }
    if (validationStatus) {
      params = params.append('validationStatus', validationStatus);
    }
    if (sortBy) {
      params = params.append('sortBy', sortBy);
    }
    if (sortOrder) {
      params = params.append('sortOrder', sortOrder);
    }

    return this.http.get<IPagedMeasureReportSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports`, { params })
      .pipe(
        map((response: IPagedMeasureReportSummary) => {
          //revert back to zero based paging
          if (response) {
            response.metadata.pageNumber--;
          }
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getMeasureReportResourceDetails(facilityId: string, measureReportId: string, resourceType: string | null, pageNumber: number, pageSize: number): Observable<IPagedResourceSummary> {

    //javascript based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let queryString: string = `?pageNumber=${pageNumber}&pageSize=${pageSize}`;

    if (resourceType) {
      queryString += `&resourceType=${resourceType}`;
    }

    return this.http.get<IPagedResourceSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports/${measureReportId}/resources${queryString}`)
      .pipe(
        map((response: IPagedResourceSummary) => {
          //revert back to zero based paging
          if (response) {
            response.metadata.pageNumber--;
          }
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getMeasureReportResourceTypes(facilityId: string, measureReportId: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports/${measureReportId}/resource-types`)
      .pipe(
        map((response: string[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }

  getReportSubmissionStatuses(): Observable<string[]> {
    return this.http.get<string[]>(`${this.appConfigService.config?.baseApiUrl}/report/report-submission-statuses`)
      .pipe(
        map((response: string[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          this.errorHandler.handleError(error);
          return [];
        })
      );
  }

  getReportValidationStatuses(): Observable<string[]> {
    return this.http.get<string[]>(`${this.appConfigService.config?.baseApiUrl}/report/report-validation-statuses`)
      .pipe(
        map((response: string[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          this.errorHandler.handleError(error);
          return [];
        })
      );
  }

  getCategoryIssuesPage(facilityId: string, reportId: string, categoryId: string,
                       pageNumber: number, pageSize: number): Observable<ICategoryIssuePage> {
    return this.http.get<ICategoryIssuePage>(
      `${this.appConfigService.config?.baseApiUrl}/validation/facilities/${encodeURIComponent(facilityId)}/reports/${encodeURIComponent(reportId)}/categories/${encodeURIComponent(categoryId)}/issues`,
      {params: {pageNumber, pageSize}}
    );
  }

  getReportCategoryCounts(facilityId: string, reportId: string): Observable<IReportCategoryCounts> {
    return this.http.get<IReportCategoryCounts>(
      `${this.appConfigService.config?.baseApiUrl}/validation/facilities/${encodeURIComponent(facilityId)}/reports/${encodeURIComponent(reportId)}/category-counts`
    );
  }
}
