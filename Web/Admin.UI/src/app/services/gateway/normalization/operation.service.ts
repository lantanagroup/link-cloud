import {HttpClient, HttpErrorResponse} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {IOperationModel, PagedConfigModel} from "../../../interfaces/normalization/operation-get-model.interface";
import {ISaveOperationModel, OperationType} from "../../../interfaces/normalization/operation-save-model.interface";
import { IPagedOperationModel } from 'src/app/components/tenant/global-operations/models/opeation-model';

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
    return this.http.get<PagedConfigModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations?FacilityId=${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched configuration.`)),
        map((response: PagedConfigModel) => {
          return response.Records;
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

  static getOperationTypes(): string[] {
    return Object.values(OperationType)
      .filter(value => typeof value === 'string' && value !== 'None') as string[];
  }

  searchGlobalOperations(
    facilityId: string | null,
    operationType: string | null,
    resourceType: string | null,
    operationId: string | null,
    includeDisabled: boolean | null,
    sortBy: string | null,
    sortOrder: 'asc' | 'desc' | null,
    pageSize: number,
    pageNumber: number
  ): Observable<IPagedOperationModel> {
    
    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let queryString: string = `pageNumber=${pageNumber}&pageSize=${pageSize}`;

    //add filters to query string
    // if(facilityId) {
    //     queryString += `&facilityId=${encodeURIComponent(facilityId)}`;
    // }
    if(operationType) {
        queryString += `&operationType=${encodeURIComponent(operationType)}`;
    }
    if(resourceType) {
        queryString += `&resourceType=${encodeURIComponent(resourceType)}`;
    }
    if(operationId) {
        queryString += `&operationId=${encodeURIComponent(operationId)}`;
    }
    if(includeDisabled !== null) {
        queryString += `&includeDisabled=${includeDisabled}`;
    }
    if(sortBy) {
        queryString += `&sortBy=${encodeURIComponent(sortBy)}`;
    }
    if(sortOrder) {
        queryString += `&sortOrder=${encodeURIComponent(sortOrder)}`;
    }  
  
    //temporary until api is updated
    queryString += `&facilityId=${encodeURIComponent("TestFacilityOne")}`;

    return this.http.get<IPagedOperationModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations?${queryString}`)
      .pipe(
        map((response: IPagedOperationModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;

          // parse the operationJson field to parsedOperationJson
          response.records.forEach(record => {
            try {
              const parsedJson = JSON.parse(record.operationJson);
              record.parsedOperationJson = {
                operationType: parsedJson.OperationType,
                name: parsedJson.Name,
                description: parsedJson.Description,
                sourceFhirPath: parsedJson.SourceFhirPath,
                targetFhirPath: parsedJson.TargetFhirPath
              };
            } catch (e) {
              console.error(`Error parsing operationJson for record with id ${record.id}:`, e);            
            }
          });          
         
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
            var err = this.errorHandler.handleError(error);
            return err;
        })
      );    
  }
}
