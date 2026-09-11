import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, tap } from 'rxjs';
import { AppConfigService } from '../../app-config.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { IFacilityReportingPlan } from '../../../interfaces/dmrp/facility-reporting-plan.interface';

@Injectable({
  providedIn: 'root'
})
export class ReportingPlanService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  private get reportingPlanApiPath(): string {
    return `${this.appConfigService.config?.baseApiUrl}/dmrp/reporting-plans`;
  }

  /**
   * All reporting plans recorded for the facility, every period and both reporting states. The
   * endpoint is unpaged by design (one row per measure per period) and answers an unknown
   * facility with an empty list.
   */
  getReportingPlansForFacility(facilityId: string): Observable<IFacilityReportingPlan[]> {
    return this.http.get<IFacilityReportingPlan[]>(`${this.reportingPlanApiPath}/facilities/${encodeURIComponent(facilityId)}`)
      .pipe(
        tap(_ => console.log(`fetched facility reporting plans.`)),
        catchError(this.handleError.bind(this))
      );
  }

  /**
   * The toastr is suppressed: the reporting plans section shows its own inline error with a
   * retry, and a toast on top of that would say the same thing twice.
   */
  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err, false);
  }
}
