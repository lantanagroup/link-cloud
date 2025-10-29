import { HttpClient, HttpErrorResponse, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { catchError, map, Observable } from "rxjs";
import { AppConfigService } from "src/app/services/app-config.service";
import { IPagedMeasureReportSummary, IPagedReportListSummary, IPagedResourceSummary, IValidationIssue, IValidationIssueCategorySummary, IValidationIssuesSummary, IReportListSummary } from "./report-view.interface";
import { ErrorHandlingService } from "src/app/services/error-handling.service";
import { IApiResponse } from "src/app/interfaces/api-response.interface";


@Injectable({
  providedIn: 'root'
})
export class FacilityViewService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  downloadReport(facilityId: string, reportId: string) {
    window.location.href = `${this.appConfigService.config?.baseApiUrl}/Submission/${facilityId}/${reportId}`;
  }

  getReportSummaryList(facilityId: string, pageNumber: number, pageSize: number): Observable<IPagedReportListSummary> {
    //javascript based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    return this.http.get<IPagedReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/aggregate/reports/summaries?facilityId=${facilityId}&pageNumber=${pageNumber}&pageSize=${pageSize}`)
      .pipe(
        map((response: IPagedReportListSummary) => {
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
    return this.http.get<IReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}?reportId=${reportId}`)
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

  getReportIssues(facilityId: string, reportId: string): Observable<IValidationIssue[]> {
    return this.http.get<IValidationIssue[]>(`${this.appConfigService.config?.baseApiUrl}/validation/result/${facilityId}/${reportId}`)
      .pipe(
        map((response: IValidationIssue[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          this.errorHandler.handleError(error);
          return [];
        })
      );
  }

  getReportIssuesSummary(issues: IValidationIssue[]): Observable<IValidationIssueCategorySummary[]> {
    return this.http.post<IValidationIssueCategorySummary[]>(`${this.appConfigService.config?.baseApiUrl}/validation/$categorize?summarize=true`, issues)
      .pipe(
        map((response: IValidationIssueCategorySummary[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          this.errorHandler.handleError(error);
          return [];
        })
      )
  }
}
