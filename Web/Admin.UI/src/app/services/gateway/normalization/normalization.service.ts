import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, catchError, map, tap} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {INormalizationModel} from "../../../interfaces/normalization/normalization-model.interface";

@Injectable({
  providedIn: 'root'
})
export class NormalizationService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  createNormalizationConfiguration(facilityId: string, normalization: INormalizationModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization`, normalization)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateNormalizationConfiguration(facilityId: string, normalization: INormalizationModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization/${facilityId}`, normalization)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getNormalizationConfiguration(facilityId: string): Observable<INormalizationModel> {
    return this.http.get<INormalizationModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        map((response: INormalizationModel) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

}
