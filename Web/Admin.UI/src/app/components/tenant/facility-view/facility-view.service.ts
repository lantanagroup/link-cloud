import { HttpClient, HttpErrorResponse } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { catchError, map, Observable } from "rxjs";
import { AppConfigService } from "src/app/services/app-config.service";
import { IPagedReportListSummary } from "./report-list-summary.interface";
import { ErrorHandlingService } from "src/app/services/error-handling.service";


@Injectable({
  providedIn: 'root'
})
export class FacilityViewService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }


   getReportSummaryList(facilityId: string, pageNumber: number, pageSize: number): Observable<IPagedReportListSummary> {
      return this.http.get<IPagedReportListSummary>(`${this.appConfigService.config?.baseApiUrl}/aggregate/reports/summaries?facilityId=${facilityId}&pageNumber=${pageNumber}&pageSize=${pageSize}`)
        .pipe(
            map((response: IPagedReportListSummary) => {
                return response;
            }),
            catchError(this.handleError.bind(this))
        );
    }

    private handleError(err: HttpErrorResponse) {
        return this.errorHandler.handleError(err);
    }
}