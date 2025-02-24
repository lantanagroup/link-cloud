import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {HttpClient, HttpErrorResponse, HttpParams} from '@angular/common/http';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {PagedUserModel} from "../../../models/user/paged-user-model.model";
import {RoleModel} from "../../../models/role/role-model.model";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";
import {UserModel} from "../../../models/user/user-model.model";
import {IApiResponse} from "../../../interfaces/response.interface";

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  getUsers(searchText: string, filterFacilityBy: string, filterRoleBy: string, filterClaimBy: string,
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

    return this.http.get<PagedUserModel>(`${this.baseApiPath}/account/users`, {params})
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

  getAllRoles(): Observable<RoleModel[]> {
    return this.http.get<RoleModel[]>(`${this.baseApiPath}/account/role`,)
      .pipe(
        tap(_ => console.log(`fetched roles logs.`)),
        map((response: RoleModel[]) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  createUser(user: UserModel): Observable<IApiResponse> {
    return this.http.post<IApiResponse>(`${this.baseApiPath}/account/user`, user)
      .pipe(
        tap(_ => console.log(`add user.`)),
        map((response) => {
          return response;
        }),
        catchError(this.handleError.bind(this))
      )
  }

  updateUser(user: UserModel): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseApiPath}/account/user/${user.id}`, user)
      .pipe(
        tap(_ => console.log(`update user.`)),
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
