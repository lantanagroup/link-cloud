import {
  HttpClient
}

from '@angular/common/http';

import {
  Injectable
}

from '@angular/core';

import {
  ErrorHandlingService
}

from '../../error-handling.service';

import {
  Observable,
  catchError,
  map,
  tap
}

from 'rxjs';

import {
  IEntityCreatedResponse
}

from 'src/app/interfaces/entity-created-response.model';

import {
  AppConfigService
}

from '../../app-config.service';

import {
  IValidationConfiguration
}

from "../../../interfaces/validation/validation-configuration.interface";

import {
  IMeasureDefinitionConfigModel
}

from "../../../interfaces/measure-definition/measure-definition-config-model.interface";

import {
  Artifact
}

from "../../../interfaces/validation/artifact.interface";

import {
  IValidationCategory,
  IValidationRule
}

from "../../../interfaces/validation/validation-category.interface";

@Injectable( {
    providedIn: 'root'
  }

) export class ValidationService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {}

  updateValidationConfiguration(validationConfiguration: IValidationConfiguration): Observable<IEntityCreatedResponse> {

    if ( !validationConfiguration.type || !validationConfiguration.name) {
      throw new Error('Type and name are required for validation configuration');
    }

    const sanitizedType=encodeURIComponent(validationConfiguration.type);
    const sanitizedName=encodeURIComponent(validationConfiguration.name);

    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/validation/artifact/${sanitizedType}/${sanitizedName}`, validationConfiguration.content, {
      headers: {
        'Content-Type': 'application/octet-stream'
      }
    }) .pipe(tap(_=> console.log(`Request for configuration update was sent.`)),
      map((response: IEntityCreatedResponse)=> {
          return response;
        }

      ),
      catchError((error)=> this.errorHandler.handleError(error)))
  }

  getValidationConfiguration(): Observable<Artifact[]> {

    return this.http.get<Artifact[]>(`${this.appConfigService.config?.baseApiUrl}/validation/artifact`) .pipe(tap(_=> console.log(`Fetched configuration.`)),
      catchError((error)=> this.errorHandler.handleError(error)))
  }

  getValidationCategory(id: string): Observable<IValidationCategory> {
    return this.http.get<IValidationCategory>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}`).pipe(
      catchError((error) => this.errorHandler.handleError(error))
    );
  }

  updateValidationCategory(id: string, category: IValidationCategory): Observable<IValidationCategory> {
    return this.http.put<IValidationCategory>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}`, category).pipe(
      catchError((error) => this.errorHandler.handleError(error))
    );
  }

  getValidationCategories(): Observable<IValidationCategory[]> {
    return this.http.get<IValidationCategory[]>(`${this.appConfigService.config?.baseApiUrl}/validation/category`) .pipe(tap(_=> console.log(`Fetched validation categories.`)),
      catchError((error)=> this.errorHandler.handleError(error)));
  }

  getValidationCategoryRuleHistory(id: string): Observable<IValidationRule[]> {
    return this.http.get<IValidationRule[]>(`${this.appConfigService.config?.baseApiUrl}/validation/category/${id}/rule/history`) .pipe(tap(_=> console.log(`Fetched validation category rule history.`)),
      catchError((error)=> this.errorHandler.handleError(error)));
  }
}