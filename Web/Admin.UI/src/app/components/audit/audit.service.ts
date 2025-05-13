import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { PagedAuditModel } from '../../models/audit/paged-audit-model.model';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { AppConfigService } from 'src/app/services/app-config.service';

@Injectable({
  providedIn: 'root'
})
export class AuditService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseApiPath: string = `${this.appConfigService.config?.baseApiUrl}`;

  searchLogs(
    searchText: string | null, 
    filterFacilityBy: string | null, 
    filterCorrelationBy: string | null, 
    filterServiceBy: string | null,
    filterActionBy: string | null, 
    filterUserBy: string | null, 
    sortBy: string | null,
    pageSize: number, 
    pageNumber: number): Observable<PagedAuditModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let queryString: string = `pageNumber=${pageNumber}&pageSize=${pageSize}`;

        //add filters to query string
        if(searchText) {
            queryString += `&searchText=${searchText}`;
        }
        if(filterFacilityBy) {
            queryString += `&facility=${filterFacilityBy}`;
        }
        if(filterCorrelationBy) {
            queryString += `&correlationId=${filterCorrelationBy}`;
        }
        if(filterServiceBy) {
            queryString += `&service=${filterServiceBy}`;
        }
        if(filterActionBy) {
            queryString += `&action=${filterActionBy}`;
        }
        if(filterUserBy) {
            queryString += `&user=${filterUserBy}`;
        }        

    return this.http.get<PagedAuditModel>(`${this.baseApiPath}/audit?${queryString}`)
    .pipe(
      map((response: PagedAuditModel) => {
        //revert back to zero based paging
        response.metadata.pageNumber--;
        return response;
      }),
      catchError((error: HttpErrorResponse) => {
        var err = this.errorHandler.handleError(error);
        return err;
    })
    )
  }

}
