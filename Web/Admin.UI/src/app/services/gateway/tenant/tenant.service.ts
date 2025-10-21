import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {HttpClient, HttpErrorResponse, HttpHeaders, HttpParams} from '@angular/common/http';
import {
  IAdHocReportRequest,
  IFacilityConfigModel,
  IScheduledReportModel,
  PagedFacilityConfigModel
} from 'src/app/interfaces/tenant/facility-config-model.interface';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {IEntityDeletedResponse} from "../../../interfaces/entity-deleted-response.interface";
import { IVendor, IVendorVersion } from 'src/app/interfaces/tenant/vendor-interface';

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }


  createFacility(facilityId: string, facilityName: string, timeZone: string, scheduledReports: IScheduledReportModel, vendor?: IVendorVersion): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      facilityId: facilityId,
      facilityName: facilityName,
      timeZone: timeZone,
      vendor: vendor ? { id: vendor.vendorId, name: vendor.vendorName } : undefined,
      vendorVersionId: vendor?.id,
      scheduledReports: scheduledReports
    };

    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateFacility(id: string, facilityId: string, facilityName: string, timeZone: string, scheduledReports: IScheduledReportModel, vendor?: IVendorVersion): Observable<IEntityCreatedResponse> {
    let facility: IFacilityConfigModel = {
      id: id,
      facilityId: facilityId,
      facilityName: facilityName,
      timeZone: timeZone,
      vendor: vendor ? { id: vendor.vendorId, name: vendor.vendorName } : undefined,
      vendorVersionId: vendor?.id,
      scheduledReports: scheduledReports
    };

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`, facility)
      .pipe(
        tap(_ => console.log(`Request for facility update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  deleteFacilityConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Delete Facility configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  softDeleteFacilityConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/facility/softDelete/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Delete Facility configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  restoreFacilityConfiguration(facilityId: string): Observable<void> {
    return this.http.patch<void>(`${this.appConfigService.config?.baseApiUrl}/facility/restore/${facilityId}`, {})
      .pipe(
        tap(_ => console.log(`Restore Facility configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  getFacilityConfiguration(facilityId: string): Observable<IFacilityConfigModel> {
    return this.http.get<IFacilityConfigModel>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched facility configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  checkFacility(facilityId: string): Observable<boolean> {
    // Replace this URL with your API endpoint
    return this.http.get<boolean>(`${this.appConfigService.config?.baseApiUrl}/facility/${facilityId}`)
      .pipe(
        catchError(() => of(false)) // Return false if there's an error
      );
  }

  getAllFacilities(includeDeleted = false): Observable<Record<string, string>> {
    const params = new HttpParams().set('includeDeleted', includeDeleted.toString());
    return this.http.get<Record<string, string>>(`${this.appConfigService.config?.baseApiUrl}/facility/list`, { params })
      .pipe(
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  autocompleteFacilities(search: string | null, includeDeleted = false): Observable<Record<string, string>> {
    const headers = new HttpHeaders({'X-Skip-Loading': 'true'});
    const params = new HttpParams().set('search', search || '').set('includeDeleted', includeDeleted.toString());
    return this.http.get<Record<string, string>>(`${this.appConfigService.config?.baseApiUrl}/facility/list`, {
      headers,
      params
    })
      .pipe(
        catchError((error) => this.errorHandler.handleError(error))
      )
  }


  listFacilities(facilityId: string, facilityName: string, timeZone: string, vendorId: string, sortBy: string, sortOrder: number, pageSize: number, pageNumber: number, showDeleted: boolean): Observable<PagedFacilityConfigModel> {

    //javascript based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let params = new HttpParams()
      .set('facilityId', facilityId)
      .set('facilityName', facilityName)
      .set('sortBy', sortBy)
      .set('sortOrder', sortOrder)
      .set('pageSize', pageSize)
      .set('pageNumber', pageNumber)
      .set('includeDeleted', showDeleted.toString());

    if (timeZone) { params = params.set('timeZone', timeZone); }
    if (vendorId) { params = params.set('vendor.id', vendorId); }

    return this.http.get<PagedFacilityConfigModel>(`${this.appConfigService.config?.baseApiUrl}/facility`, {params})
      .pipe(
        tap(_ => console.log(`Fetched facilities.`)),
        map((response: PagedFacilityConfigModel) => {
          //revert back to zero based paging
          if (response) {
            response.metadata.pageNumber--;
          }
          return response;
        }),
        catchError((err) => this.handleError(err))
      )
  }

  generateAdHocReport(facilityId: string, adHocReportRequest: IAdHocReportRequest) {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/Facility/${facilityId}/AdHocReport`, adHocReportRequest)
      .pipe(
        tap(_ => console.log(`Request for adHoc reporting was sent.`)),
        map((response: any) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  regenerateReport(facilityId: string, reportId: string, bypassSubmission?: boolean) {
    // Include bypassValue if provided
    const adHocReportRequest: any = {reportId};
    if (bypassSubmission) {
      adHocReportRequest.bypassSubmission = bypassSubmission;
    }

    return this.http.post<IEntityCreatedResponse>(
      `${this.appConfigService.config?.baseApiUrl}/Facility/${facilityId}/RegenerateReport`,
      adHocReportRequest
    ).pipe(
      tap(_ => console.log(`Request to regenerate report was sent.`)),
      map((response: any) => response),
      catchError((error) => this.errorHandler.handleError(error))
    );
  }

  getVendors(): Observable<IVendor[]> {
    return this.http.get<IVendor[]>(`${this.appConfigService.config?.baseApiUrl}/vendor`)
      .pipe(
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getVendorVersions(): Observable<IVendorVersion[]> {
    return this.http.get<IVendorVersion[]>(`${this.appConfigService.config?.baseApiUrl}/VendorVersion`)
      .pipe(
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

}
