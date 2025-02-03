import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import {HttpClient, HttpErrorResponse, HttpParams} from '@angular/common/http';
import { Observable, catchError, map, tap, of } from 'rxjs';
import { AppConfigService } from '../../app-config.service';
import {PagedUserModel} from "../../../models/user/paged-user-model.model";

@Injectable({
  providedIn: 'root'
})
export class UserService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  list(searchText: string, filterFacilityBy: string, filterRoleBy: string, filterClaimBy: string,
       includeDeactivatedUsers: boolean, includeDeletedUsers: boolean, sortBy: string, pageSize: number, pageNumber: number): Observable<PagedUserModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    const params = new HttpParams()
      .set('searchText', searchText)
      .set('filterFacilityBy', filterFacilityBy)
      .set('filterRoleBy', filterRoleBy)
      .set('filterClaimBy', filterClaimBy)
      .set('includeDeactivatedUsers', includeDeactivatedUsers)
      .set('includeDeletedUsers', includeDeletedUsers)
      .set('sortBy', sortBy)
      .set('pageSize', pageSize)
      .set('pageNumber', pageNumber);

    return this.http.get<PagedUserModel>(`${this.baseApiPath}/account/users`, { params })
      .pipe(
        tap(_ => console.log(`fetched user logs.`)),
        map((response: PagedUserModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }
}
