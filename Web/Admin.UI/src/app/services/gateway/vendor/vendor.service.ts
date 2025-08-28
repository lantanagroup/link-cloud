import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {HttpClient, HttpErrorResponse} from '@angular/common/http';
import {Observable, catchError, map, tap} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {IApiResponse} from "../../../interfaces/api-response.interface";
import {IVendorConfigModel} from "../../../interfaces/vendor/vendor-config-model.interface";

@Injectable({
  providedIn: 'root'
})
export class VendorService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  getVendors(): Observable<any> {
    return this.http.get<any>(`${this.baseApiPath}/normalization/Vendor/vendors`,)
      .pipe(
        tap(_ => console.log(`fetched vendors.`)),
        catchError(this.handleError.bind(this))
      )
  }

  createVendor(vendorId: IVendorConfigModel): Observable<any> {
    return this.http.post<any>(`${this.baseApiPath}/normalization/Vendor/${vendorId}`, "")
      .pipe(
        tap(_ => console.log(`add user.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  deleteVendor(vendorId: string): Observable<IApiResponse> {
    return this.http.delete<IApiResponse>(`${this.baseApiPath}/normalization/Vendor/${vendorId}`)
      .pipe(
        tap(_ => console.log(`delete user.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
