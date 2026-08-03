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

  /**
   * The vendor name travels in the route, not a body. Typed as a string because that is what
   * callers pass -- it was previously declared as IVendorConfigModel and interpolated straight
   * into the URL, which produced "[object Object]" for anything but a bare string.
   */
  createVendor(name: string): Observable<any> {
    return this.http.post<any>(`${this.baseApiPath}/normalization/Vendor/${encodeURIComponent(name)}`, "")
      .pipe(
        tap(_ => console.log(`created vendor.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  /**
   * Persists changes to a vendor, including its Key Vault secret id.
   *
   * PROVISIONAL ROUTE. No update endpoint exists yet: the Vendor model is moving out of
   * Normalization and into Tenant under LEGLINK-743, whose acceptance criteria cover list,
   * add and delete but not update, so this operation is owned by neither ticket. The route
   * below mirrors the sibling delete endpoint so the shape is a reasonable guess, and this
   * method is deliberately the only place in the UI that knows it -- when the contract is
   * confirmed, this body is the sole edit required. Until then it is exercised through mocks.
   */
  updateVendor(vendor: IVendorConfigModel): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseApiPath}/normalization/Vendor/${encodeURIComponent(vendor.id)}`, vendor)
      .pipe(
        tap(_ => console.log(`updated vendor.`)),
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
