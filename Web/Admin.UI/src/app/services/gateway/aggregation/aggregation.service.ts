import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class AggregationService {
  constructor(
    private http: HttpClient,
    public appConfigService: AppConfigService
  ) {}

  softDeleteFacility(facilityId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.appConfigService.config?.baseApiUrl}/aggregate/facility/${encodeURIComponent(facilityId)}`
    );
  }

  restoreFacility(facilityId: string): Observable<void> {
    return this.http.patch<void>(
      `${this.appConfigService.config?.baseApiUrl}/aggregate/facility/${encodeURIComponent(facilityId)}/restore`,
      null
    );
  }

  softDeleteReport(reportScheduleId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.appConfigService.config?.baseApiUrl}/aggregate/reports/${encodeURIComponent(reportScheduleId)}`
    );
  }

  restoreReport(reportScheduleId: string): Observable<void> {
    return this.http.patch<void>(
      `${this.appConfigService.config?.baseApiUrl}/aggregate/reports/${encodeURIComponent(reportScheduleId)}/restore`,
      null
    );
  }

  abortReport(reportScheduleId: string): Observable<void> {
    return this.http.post<void>(
      `${this.appConfigService.config?.baseApiUrl}/aggregate/reports/${encodeURIComponent(reportScheduleId)}/abort`,
      null
    );
  }


}
