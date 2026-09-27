import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable, catchError} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {ErrorHandlingService} from '../../error-handling.service';

export interface FacilityLocationMapping {
  id: string;
  localCodeSystem: string;
  localCode: string;
  hslocId: string | null;
  hslocCode: string | null;
}

export interface FacilityLocation {
  id: string;
  locationId: string;
  partOfId: string | null;
  locationName: string | null;
  locationAlias: string | null;
  mappings: FacilityLocationMapping[];
}

export interface FacilityLocationsResponse {
  records: FacilityLocation[];
  metadata: {pageSize: number; pageNumber: number; totalCount: number; totalPages: number};
}

@Injectable({providedIn: 'root'})
export class FacilityLocationsService {
  constructor(private http: HttpClient, private appConfig: AppConfigService,
              private errorHandler: ErrorHandlingService) {}

  getForFacility(facilityId: string): Observable<FacilityLocationsResponse> {
    const url = `${this.appConfig.config?.baseApiUrl}/normalization/facility-locations/facilities/${encodeURIComponent(facilityId)}/locations`;
    return this.http.get<FacilityLocationsResponse>(url)
      .pipe(catchError(error => this.errorHandler.handleError(error)));
  }
}