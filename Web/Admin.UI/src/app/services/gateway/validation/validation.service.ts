import { IValidationIssueCategory, IValidationRule } from 'src/app/components/tenant/facility-view/report-view.interface';
import { Observable, catchError, map, tap } from 'rxjs';

import { AppConfigService } from '../../app-config.service';
import { Artifact } from "../../../interfaces/validation/artifact.interface";
import { ErrorHandlingService } from '../../error-handling.service';
import { HttpClient } from '@angular/common/http';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IValidationConfiguration } from "../../../interfaces/validation/validation-configuration.interface";
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ValidationService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  updateValidationConfiguration(validationConfiguration: IValidationConfiguration): Observable<IEntityCreatedResponse> {

    if (!validationConfiguration.type || !validationConfiguration.name) {
      throw new Error('Type and name are required for validation configuration');
    }
    const sanitizedType = encodeURIComponent(validationConfiguration.type);
    const sanitizedName = encodeURIComponent(validationConfiguration.name);

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/validation/artifact/${sanitizedType}/${sanitizedName}`, validationConfiguration.content, {
      headers: { 'Content-Type': 'application/octet-stream' }
    })
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getValidationConfiguration(): Observable<Artifact[]> {

    return this.http.get<Artifact[]>(`${this.appConfigService.config?.baseApiUrl}/validation/artifact`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getValidationCategories(): Observable<IValidationIssueCategory[]> {

    return this.http.get<IValidationIssueCategory[]>(`${this.appConfigService.config?.baseApiUrl}/validation/category`).pipe(
      tap(_ => console.log(`Fetched categories.`)),
      catchError((error) => this.errorHandler.handleError(error))
    )
  }

  getValidationCategory(id: string): Observable<IValidationIssueCategory> {

    return this.http.get<IValidationIssueCategory>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}`).pipe(
      catchError((error) => this.errorHandler.handleError(error))
    );
  }

  updateValidationCategory(id: string, category: IValidationIssueCategory): Observable<IValidationIssueCategory> {

    return this.http.put<IValidationIssueCategory>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}`, category).pipe(
      catchError((error) => this.errorHandler.handleError(error))
    );
  }

  getValidationCategoryRuleHistory(id: string): Observable<IValidationRule[]> {
    return this.http.get<IValidationRule[]>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}/rule/history`) .pipe(tap(_=> console.log(`Fetched validation category rule history.`)),
      catchError((error)=> this.errorHandler.handleError(error)));
  }
}
