import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {IOperationModel} from "../../../interfaces/normalization/operation-get-model.interface";
import {ISaveOperationModel} from "../../../interfaces/normalization/operation-save-model.interface";

@Injectable({
  providedIn: 'root'
})
export class OperationService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  createOperationConfiguration(operation: ISaveOperationModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations`, operation)
      .pipe(
        tap(_ => console.log(`Request for configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  updateOperationConfiguration(operation: ISaveOperationModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations`, operation)
      .pipe(
        tap(_ => console.log(`Request for configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error))
      )
  }

  getOperationConfiguration(facilityId: string): Observable<IOperationModel[]> {
    return this.http.get<IOperationModel[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations?FacilityId=${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        map((response: IOperationModel[]) => {
          return response;
        }),
        catchError((error) => this.errorHandler.handleError(error, false))
      )
  }

  getResourceTypes(): Observable<string[]> {
    const resourceTypes: string[] = [
      'Patient',
      'Encounter',
      'Observation',
      'Condition',
      'Medication',
      'AllergyIntolerance',
      'Immunization',
      'CarePlan',
      'Procedure',
      'ClinicalImpression',
      'Practitioner',
      'Organization',
      'Appointment',
      'DiagnosticReport',
      'Coverage',
      'Questionnaire',
      'DocumentReference',
      'Device',
      'Location',
      'Specimen'
    ];
    return of(resourceTypes);
  }
}
