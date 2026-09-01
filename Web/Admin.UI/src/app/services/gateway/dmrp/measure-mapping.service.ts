import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, tap } from 'rxjs';
import { AppConfigService } from '../../app-config.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { Frequency, IMeasureMapping, IPagedMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

@Injectable({
  providedIn: 'root'
})
export class MeasureMappingService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  private get measureMappingApiPath(): string {
    return `${this.appConfigService.config?.baseApiUrl}/dmrp/measure-mappings`;
  }

  searchMeasureMappings(
    measure: string,
    dqm: string,
    frequency: Frequency | null,
    sortBy: string,
    sortOrder: number,
    pageSize: number,
    pageNumber: number
  ): Observable<IPagedMeasureMapping | null> {
    // The API's PageNumber is 1-based; the Material paginator is 0-based.
    pageNumber = pageNumber + 1;

    let params = new HttpParams()
      .set('sortBy', sortBy)
      .set('sortOrder', sortOrder)
      .set('pageSize', pageSize)
      .set('pageNumber', pageNumber);

    if (measure) { params = params.set('measure', measure); }
    if (dqm) { params = params.set('dqm', dqm); }
    if (frequency) { params = params.set('frequency', frequency); }

    return this.http.get<IPagedMeasureMapping>(`${this.measureMappingApiPath}/search`, { params })
      .pipe(
        tap(_ => console.log(`fetched measure mappings.`)),
        map((response) => {
          // The API returns 204 No Content (a null body) when the search has no matches.
          if (response) {
            response.metadata.pageNumber--;
          }
          return response;
        }),
        catchError(this.handleError.bind(this))
      );
  }

  createMeasureMapping(measureMapping: IMeasureMapping): Observable<IMeasureMapping> {
    return this.http.post<IMeasureMapping>(this.measureMappingApiPath, this.toRequestBody(measureMapping))
      .pipe(
        tap(_ => console.log(`created measure mapping.`)),
        catchError(this.handleSaveError.bind(this))
      );
  }

  updateMeasureMapping(measureMapping: IMeasureMapping): Observable<IMeasureMapping> {
    return this.http.put<IMeasureMapping>(`${this.measureMappingApiPath}/${encodeURIComponent(measureMapping.id)}`, this.toRequestBody(measureMapping))
      .pipe(
        tap(_ => console.log(`updated measure mapping.`)),
        catchError(this.handleSaveError.bind(this))
      );
  }

  deleteMeasureMapping(id: string): Observable<any> {
    return this.http.delete(`${this.measureMappingApiPath}/${encodeURIComponent(id)}`)
      .pipe(
        tap(_ => console.log(`deleted measure mapping.`)),
        catchError(this.handleError.bind(this))
      );
  }

  private toRequestBody(measureMapping: IMeasureMapping) {
    return {
      measure: measureMapping.measure,
      dqm: measureMapping.dqm,
      frequency: measureMapping.frequency
    };
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }

  /**
   * Save failures are presented by the mapping dialog, which shows a snackbar and stays open so
   * the admin's input is not thrown away. Suppressing the toastr here leaves the dialog as the
   * single surface, matching VendorService's save-vs-list/delete split.
   */
  private handleSaveError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err, false);
  }
}
