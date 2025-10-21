import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {HttpClient, HttpErrorResponse} from '@angular/common/http';
import {Observable, catchError, map, tap} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {IApiResponse} from "../../../interfaces/api-response.interface";
import {IVendorConfigModel} from "../../../interfaces/vendor/vendor-config-model.interface";
import {IVendor} from "../../../interfaces/tenant/vendor-interface";

@Injectable({
  providedIn: 'root'
})
export class VendorService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  private get vendorApiPath(): string {
    return `${this.baseApiPath}/vendor`;
  }

  getVendors(): Observable<IVendorConfigModel[]> {
    return this.http.get<IVendor[]>(this.vendorApiPath)
      .pipe(
        tap(_ => console.log(`fetched vendors.`)),
        map(vendors => (vendors ?? []).map(vendor => this.toConfigModel(vendor))),
        catchError(this.handleError.bind(this))
      )
  }

  createVendor(vendor: IVendorConfigModel): Observable<any> {
    return this.http.post<any>(this.vendorApiPath, this.toRequestBody(vendor))
      .pipe(
        tap(_ => console.log(`created vendor.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleSaveError.bind(this))
      )
  }

  updateVendor(vendor: IVendorConfigModel): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.vendorApiPath}/${encodeURIComponent(vendor.id)}`, this.toRequestBody(vendor))
      .pipe(
        tap(_ => console.log(`updated vendor.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleSaveError.bind(this))
      )
  }

  deleteVendor(vendorId: string): Observable<IApiResponse> {
    return this.http.delete<IApiResponse>(`${this.vendorApiPath}/${encodeURIComponent(vendorId)}`)
      .pipe(
        tap(_ => console.log(`delete user.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  private toRequestBody(vendor: IVendorConfigModel) {
    return {
      name: vendor.name,
      authentication: {
        signingKeySecretId: vendor.secretId ?? null
      }
    };
  }

  private toConfigModel(vendor: IVendor): IVendorConfigModel {
    return {
      id: vendor.id,
      name: vendor.name,
      secretId: vendor.authentication?.signingKeySecretId ?? null
    };
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

  /**
   * Save failures are presented by the config dialog, which shows a snackbar and stays open so
   * the admin's input is not thrown away. ErrorHandlingService raises its own toastr by default,
   * so one failed save produced two messages; suppressing the toastr here leaves the dialog as
   * the single surface. List and delete keep the toastr, having no dialog to carry the news.
   *
   * The rethrown error still carries the sanitized `message` the dialog displays -- that is set
   * regardless of whether the toastr is shown.
   */
  private handleSaveError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err, false);
  }

}
