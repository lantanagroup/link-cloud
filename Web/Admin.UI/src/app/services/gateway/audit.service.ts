import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map, retry, tap } from 'rxjs/operators';
import { PagedAuditModel } from '../../models/audit/paged-audit-model.model';
import { ErrorHandlingService } from '../error-handling.service';
import { AppConfigService } from '../app-config.service';

@Injectable({
  providedIn: 'root'
})
export class AuditService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  list(searchText: string, filterFacilityBy: string, filterCorrelationBy: string, filterServiceBy: string,
    filterActionBy: string, filterUserBy: string, sortBy: string, pageSize: number, pageNumber: number): Observable<PagedAuditModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    return this.http.get<PagedAuditModel>(`${this.baseApiPath}/audit?searchText=${searchText}&filterFacilityBy=${filterFacilityBy}&filterCorrelationBy=${filterCorrelationBy}&filterServiceBy=${filterServiceBy}&filterActionBy=${filterActionBy}&filterUserBy=${filterUserBy}&sortBy=${sortBy}&pageSize=${pageSize}&pageNumber=${pageNumber}`)
    .pipe(
      tap(_ => console.log(`fetched audit logs.`)),
      map((response: PagedAuditModel) => {
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
