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
                catchError(this.handleError.bind(this))
            );
    }
    
    getReportSummary(facilityId: string, reportId: string): Observable<IReportListSummary> {
        return this.http.get<IReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}?reportId=${reportId}`)
            .pipe(
                map((response: IReportListSummary) => {
                    return response;
                }),
                catchError(this.handleError.bind(this))
            );
    }

    getMeasureReportSummaryList(facilityId: string, reportId: string, pageNumber: number, pageSize: number): Observable<IPagedMeasureReportSummary> {
        
        //javascript based paging is zero based, so increment page number by 1
        pageNumber = pageNumber + 1;
        
        return this.http.get<IPagedMeasureReportSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports?reportId=${reportId}&pageNumber=${pageNumber}&pageSize=${pageSize}`)
            .pipe(
                map((response: IPagedMeasureReportSummary) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;
                    return response;
                }),
                catchError(this.handleError.bind(this))
            );
    }

    getMeasureReportResourceDetails(facilityId: string, measureReportId: string, pageNumber: number, pageSize: number): Observable<IPagedResourceSummary> {
    
        //javascript based paging is zero based, so increment page number by 1
        pageNumber = pageNumber + 1;

        return this.http.get<IPagedResourceSummary>(`${this.appConfigService.config?.baseApiUrl}/report/summaries/${facilityId}/measure-reports/${measureReportId}/resources?pageNumber=${pageNumber}&pageSize=${pageSize}`)
            .pipe(
                map((response: IPagedResourceSummary) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;
                    return response;
                }),
                catchError(this.handleError.bind(this))
            );
    }
    
    private handleError(err: HttpErrorResponse) {
        return this.errorHandler.handleError(err);
    }
}