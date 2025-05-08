import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable } from 'rxjs';
import { AppConfigService } from 'src/app/services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { AcquisitionLogSummary } from './models/acquisition-log-summary';
import { AcquisitionLog } from './models/acquisition-log';

@Injectable({
  providedIn: 'root'
})
export class AcquisitionLogService {

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseUrl = `${this.appConfigService.config?.baseApiUrl}/data/acquisition`;

  getAcquisitionLogs(showLoadingIndicator: boolean = true) : Observable<AcquisitionLogSummary[]> {
    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });
    
    if(showLoadingIndicator)
    {      
      return this.http.get<AcquisitionLogSummary[]>(this.baseUrl)
      .pipe(
        map((response: AcquisitionLogSummary[]) => {        
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
      return this.http.get<AcquisitionLogSummary[]>(this.baseUrl, { headers: headers })
      .pipe(
        map((response: AcquisitionLogSummary[]) => {        
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
    return this.http.post<any>(`${this.baseUrl}/process/${id}`, id)
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
}
