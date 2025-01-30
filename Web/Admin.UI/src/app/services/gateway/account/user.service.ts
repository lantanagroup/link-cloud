import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
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

    return this.http.get<PagedUserModel>(`${this.baseApiPath}/account/users?searchText=${searchText}&filterFacilityBy=${filterFacilityBy}&filterRoleBy=${filterRoleBy}&filterClaimBy=${filterClaimBy}&includeDeactivatedUsers=${includeDeactivatedUsers}&includeDeletedUsers=${includeDeletedUsers}&sortBy=${sortBy}&pageSize=${pageSize}&pageNumber=${pageNumber}`)
      .pipe(
        tap(_ => console.log(`fetched user logs.`)),
        map((response: PagedUserModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;
          return response;
        }),
        catchError(this.handleError)
      )
  }

  private handleError(err: HttpErrorResponse) {
    return this.errorHandler.handleError(err);
  }
}
