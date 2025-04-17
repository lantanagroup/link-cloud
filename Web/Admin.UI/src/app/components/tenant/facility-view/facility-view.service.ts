import { HttpClient, HttpErrorResponse } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { catchError, map, Observable } from "rxjs";
import { AppConfigService } from "src/app/services/app-config.service";
import { IPagedMeasureReportSummary, IPagedReportListSummary, IPagedResourceSummary, IReportListSummary } from "./report-view.interface";
import { ErrorHandlingService } from "src/app/services/error-handling.service";


@Injectable({
  providedIn: 'root'
})
export class FacilityViewService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }


    getReportSummaryList(facilityId: string, pageNumber: number, pageSize: number): Observable<IPagedReportListSummary> {
        //javascript based paging is zero based, so increment page number by 1
        pageNumber = pageNumber + 1;
        
        return this.http.get<IPagedReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/aggregate/reports/summaries?facilityId=${facilityId}&pageNumber=${pageNumber}&pageSize=${pageSize}`)
            .pipe(
                map((response: IPagedReportListSummary) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;
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
        reportStatus: string | null, validationStatus: string | null,
        pageNumber: number, pageSize: number): Observable<IPagedMeasureReportSummary> {
        
        //javascript based paging is zero based, so increment page number by 1
        pageNumber = pageNumber + 1;

        let queryString: string = `?reportId=${reportId}&pageNumber=${pageNumber}&pageSize=${pageSize}`;

        //add filters to query string
        if(patientId) {
            queryString += `&patientId=${patientId}`;
        }
        if(measureReportId) {
            queryString += `&measureReportId=${measureReportId}`;
        }
        if(measure) {
            queryString += `&measure=${measure}`;
        }
        if(reportStatus) {
            queryString += `&reportStatus=${reportStatus}`;
        }
        if(validationStatus) {
            queryString += `&validationStatus=${validationStatus}`;
        }        

        return this.http.get<IPagedMeasureReportSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports${queryString}`)
            .pipe(
                map((response: IPagedMeasureReportSummary) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;
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

        if(resourceType) {
            queryString += `&resourceType=${resourceType}`;
        }

        return this.http.get<IPagedResourceSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports/${measureReportId}/resources${queryString}`)
            .pipe(
                map((response: IPagedResourceSummary) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;
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
}