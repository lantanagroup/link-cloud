import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import {AppConfigService} from '../../services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { catchError, map, Observable } from 'rxjs';

export interface IServiceInfoModel {
  serviceName: string;
  version?: string;
  productVersion?: string;
  commit?: string;
  build?: string;
  swaggerUrl?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ServiceInfoService {
  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  getServiceInfos(): Observable<IServiceInfoModel[]> {
    return this.http.get<IServiceInfoModel[]>(`${this.baseApiPath}/info`)
      .pipe(
        map((response: IServiceInfoModel[]) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      );
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }
}
