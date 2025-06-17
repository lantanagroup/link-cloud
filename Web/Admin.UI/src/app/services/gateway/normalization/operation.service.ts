import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {IOperationModel, PagedConfigModel} from "../../../interfaces/normalization/operation-get-model.interface";
import {ISaveOperationModel} from "../../../interfaces/normalization/operation-save-model.interface";
import {IOperation} from "../../../interfaces/normalization/operation.interface";
import {OperationType} from "../../../interfaces/normalization/operation-type-enumeration";
import {CopyPropertyOperation} from "../../../interfaces/normalization/copy-property-interface";
import {
  ConditionalTransformOperation
} from "../../../interfaces/normalization/conditional-transformation-operation-interface";

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
    return this.http.get<PagedConfigModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/${facilityId}`)
      .pipe(map(rawList => rawList.records.map(this.parseOperationModel)),
        catchError((error) => this.errorHandler.handleError(error, false))
      );
  }

  parseOperationModel(op: any): IOperationModel {
    try {
      const parsedJson = JSON.parse(op.operationJson);
      let typedOperation: IOperation;
      switch (op.operationType) {
        case OperationType.CopyProperty:
          typedOperation = {
            OperationType: OperationType.CopyProperty,
            ...parsedJson
          } as CopyPropertyOperation;
          break;

        case OperationType.ConditionalTransform:
          typedOperation = {
            OperationType: OperationType.ConditionalTransform,
            ...parsedJson
          } as ConditionalTransformOperation;
          break;
        default:
          throw new Error(`Unsupported operation type: ${op.operationType}`);
      }
      return {...op, operationJson: typedOperation};
    } catch (error) {
      throw new Error(`${(error as Error).message}`);
    }
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
