import {HttpClient, HttpErrorResponse, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, catchError, map, tap, of} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {AppConfigService} from '../../app-config.service';
import {ISaveOperationModel} from "../../../interfaces/normalization/operation-save-model.interface";
import {OperationType} from "../../../interfaces/normalization/operation-type-enumeration";
import {CopyPropertyOperation} from "../../../interfaces/normalization/copy-property-interface";
import {IResource} from "../../../interfaces/normalization/resource-interface";
import { ConditionalTransformOperation } from "../../../interfaces/normalization/conditional-transformation-operation-interface";
import { IPagedOperationModel } from 'src/app/interfaces/normalization/operation-get-model.interface';
import { CodeMapOperation } from 'src/app/interfaces/normalization/code-map-operation-interface';
import {IVendor} from "../../../interfaces/normalization/vendor-interface";

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

  getResourceTypes(): Observable<string[]> {
    return this.http.get<IResource[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/resource/resources`)
      .pipe(
        map(res => res.map(r => r.resourceName)),
        catchError(err => this.errorHandler.handleError(err))
      );
  }

  static getOperationTypes(): string[] {
    return Object.values(OperationType).filter(value => typeof value === 'string' && value !== 'None') as string[];
  }

  getVendors(): Observable<IVendor[]> {
    return this.http.get<IVendor[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/vendor/vendors`);
  }

  deleteOperationByFacility(facilityId: string, operationId: string, resourceType: string): Observable<any> {
    return this.http.delete<IResource[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/facility/${facilityId}?operationId=${operationId}&resourceType=${resourceType}`)
      .pipe(
          tap(_ => console.log('Request for operation deletion by facility was sent.')),
          catchError(err => this.errorHandler.handleError(err))
      );
  }

  searchGlobalOperations(
    facilityId: string | null,
    operationType: string | null,
    resourceType: string | null,
    operationId: string | null,
    includeDisabled: boolean | null,
    vendorId: string | null,
    sortBy: string | null,
    sortOrder: 'ascending' | 'descending' | null,
    pageSize: number,
    pageNumber: number
  ): Observable<IPagedOperationModel> {

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let params: HttpParams = new HttpParams();
    params = params.set('pageNumber', pageNumber.toString());
    params = params.set('pageSize', pageSize.toString());

    //add filters to query string
    if(facilityId) {
        params = params.set('facilityId', facilityId);
    }
    if(operationType) {
        params = params.set('operationType', operationType);
    }
    if(resourceType) {
        params = params.set('resourceType', resourceType);
    }
    if(operationId) {
        params = params.set('operationId', operationId);
    }
    if(includeDisabled !== null) {
        params = params.set('includeDisabled', includeDisabled.toString());
    }
    if(vendorId !== null) {
       params = params.set('vendorId', vendorId);
    }
    if(sortBy) {
        params = params.set('sortBy', sortBy);
    }
    if(sortOrder) {
        params = params.set('sortOrder', sortOrder);
    }

    return this.http.get<IPagedOperationModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations`, { params })
      .pipe(
        map((response: IPagedOperationModel) => {
          //revert back to zero based paging
          response.metadata.pageNumber--;

          // parse the operationJson field to parsedOperationJson
          response.records.forEach(record => {
            try {
              const parsedJson = JSON.parse(record.operationJson);
              switch(record.operationType) {
                case OperationType.CopyProperty:
                  record.parsedOperationJson = parsedJson as CopyPropertyOperation;
                  break;
                case OperationType.ConditionalTransform:
                  record.parsedOperationJson = parsedJson as ConditionalTransformOperation;
                  break;
                case OperationType.CodeMap:
                  record.parsedOperationJson = parsedJson as CodeMapOperation;
                  break;
                default:
                  console.warn(`Unsupported operation type: ${record.operationType} for record with id ${record.id}`);
                  record.parsedOperationJson = parsedJson;
                  break;
              }
            } catch (e) {
              console.error(`Error parsing operationJson for record with id ${record.id}:`, e);
            }
          });

          return response;
        }),
        catchError((error: HttpErrorResponse) => {
           return  this.errorHandler.handleError(error);
        })
      );
  }
}
