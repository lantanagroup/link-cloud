import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import {AppConfigService} from '../../services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { catchError, map, Observable } from 'rxjs';
import { ILinkServiceHealthSummary } from './link-health-check/link-service-health-summary.interface';

@Injectable({
  providedIn: 'root'
})
export class MonitorService {

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  getLinkHealthCheck(): Observable<ILinkServiceHealthSummary[]> {
  
    return this.http.get<ILinkServiceHealthSummary[]>(`${this.baseApiPath}/monitor/health`)
      .pipe(
        map((response: ILinkServiceHealthSummary[]) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      );
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
