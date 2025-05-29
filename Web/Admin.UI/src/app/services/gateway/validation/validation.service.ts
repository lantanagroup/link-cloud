import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ErrorHandlingService } from '../../error-handling.service';
import { Observable, catchError, map, tap } from 'rxjs';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { AppConfigService } from '../../app-config.service';
import {IValidationConfiguration} from "../../../interfaces/validation/validation-configuration.interface";

@Injectable({
  providedIn: 'root'
})
export class ValidationService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

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
}
