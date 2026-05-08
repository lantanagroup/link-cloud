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
import {
    ConditionalTransformOperation
} from "../../../interfaces/normalization/conditional-transformation-operation-interface";
import {IOperationModel, IPagedOperationModel} from 'src/app/interfaces/normalization/operation-get-model.interface';
import {CodeMapOperation} from 'src/app/interfaces/normalization/code-map-operation-interface';
import {IVendor} from "../../../interfaces/normalization/vendor-interface";
import {IOperationSequenceModel} from "../../../interfaces/normalization/operation-sequence-get-model.interface";
import {IOperationSequenceSaveModel} from "../../../interfaces/normalization/operation-sequence-save-model.interface";
import {IOperation} from "../../../interfaces/normalization/operation.interface";


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
                catchError((error) => this.errorHandler.handleError(error, false))
            )
    }

    updateOperationConfiguration(operation: ISaveOperationModel): Observable<IEntityCreatedResponse> {
        return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations`, operation)
            .pipe(
                tap(_ => console.log(`Request for configuration update was sent.`)),
                map((response: IEntityCreatedResponse) => {
                    return response;
                }),
                catchError((error) => this.errorHandler.handleError(error, false))
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

    deleteOperationByFacility(facilityId: string, operationId: string): Observable<any> {
        return this.http.delete<IResource[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/facility/${facilityId}?operationId=${operationId}`)
            .pipe(
                tap(_ => console.log('Request for operation deletion by facility was sent.')),
            );
    }

    deleteAllOperationsByFacility(facilityId: string): Observable<any> {
      return this.http.delete(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/facility/${facilityId}`)
        .pipe(
          tap(() => console.log('All operations for facility deleted')),
          catchError((error) => {
            throw error;
          })
        );
    }

    deleteOperationByVendor(vendorName: string, operationId: string): Observable<any> {
        return this.http.delete<IResource[]>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/vendor/${vendorName}?operationId=${operationId}`)
            .pipe(
                tap(_ => console.log('Request for operation deletion by vendor was sent.')),
            );
    }

    getOperationSequences(facilityId: string, resourceType?: string): Observable<IOperationSequenceModel[]> {

        const url = `${this.appConfigService.config?.baseApiUrl}/normalization/OperationSequence`;

        let params = new HttpParams();

        if (facilityId) {
            params = params.set('facilityId', facilityId);
        }
        if (resourceType) {
            params = params.set('resourceType', resourceType);
        }

        return this.http.get<IOperationSequenceModel[]>(url, {params})
            .pipe(
                map((response: IOperationSequenceModel[]) => {
                    return response;
                }),
                catchError((error: HttpErrorResponse) => {
                    return this.errorHandler.handleError(error);
                })
            );
    }

    saveOperationSequences(facilityId: string, resourceType: string, data: IOperationSequenceSaveModel[]): Observable<any> {
        const params = new HttpParams()
            .set('facilityId', facilityId)
            .set('resourceType', resourceType);

        return this.http.post(
            `${this.appConfigService.config?.baseApiUrl}/normalization/OperationSequence`, data, {params}
        ).pipe(
            tap(_ => console.log('Request for operation sequence save was sent.')),
            catchError((error) => this.errorHandler.handleError(error, false))
        );
    }


    deleteOperationSequencesByFacilityResourceType(facilityId: string, resourceType: string): Observable<any> {
        const params = new HttpParams()
            .set('facilityId', facilityId)
            .set('resourceType', resourceType);

        return this.http.delete(
            `${this.appConfigService.config?.baseApiUrl}/normalization/OperationSequence`, {params}
        ).pipe(
            tap(_ => console.log('Delete operation sequences was successful.')),
            catchError((error) => this.errorHandler.handleError(error, false))
        );
    }

    deleteOperationSequencesByFacility(facilityId: string): Observable<any> {
        const params = new HttpParams()
            .set('facilityId', facilityId)

        return this.http.delete(
            `${this.appConfigService.config?.baseApiUrl}/normalization/OperationSequence`, {params}
        ).pipe(
            tap(_ => console.log('Delete operation sequences was successful.')),
            catchError((error) => this.errorHandler.handleError(error, false))
        );
    }


    getOperationsByFacility(facilityId: string, pageSize?: number, pageNumber?: number, vendorId?: string, resourceType?: string, ): Observable<IPagedOperationModel> {
        const url = `${this.appConfigService.config?.baseApiUrl}/normalization/operations/facility/${facilityId}`;

        //java based paging is zero based, so increment page number by 1
        pageNumber = (pageNumber ?? 0) + 1;

        let params = new HttpParams();

        if (resourceType) {
            params = params.set('resourceType', resourceType);
        }

        if (vendorId) {
            params = params.set('vendorId', vendorId);
        }

        if(pageNumber) {
          params = params.set('pageNumber', pageNumber.toString());
        }

        if (pageSize) {
          params = params.set('pageSize', pageSize.toString());
        }

        params = params.set('sortBy', "OperationType");

        params = params.set('sortOrder', "ascending");

        return this.http.get<IPagedOperationModel>(url, {params})
            .pipe(
                map((response: IPagedOperationModel) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;

                    // parse the operationJson field to parsedOperationJson
                    this.parseOperationRecords(response.records);

                    return response;
                }),
                catchError((error: HttpErrorResponse) => {
                    return this.errorHandler.handleError(error);
                })
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
        if (facilityId) {
            params = params.set('facilityId', facilityId);
        }
        if (operationType) {
            params = params.set('operationType', operationType);
        }
        if (resourceType) {
            params = params.set('resourceType', resourceType);
        }
        if (operationId) {
            params = params.set('operationId', operationId);
        }
        if (includeDisabled !== null) {
            params = params.set('includeDisabled', includeDisabled.toString());
        }
        if (vendorId !== null) {
            params = params.set('vendorId', vendorId);
        }
        if (sortBy) {
            params = params.set('sortBy', sortBy);
        }
        if (sortOrder) {
            params = params.set('sortOrder', sortOrder);
        }

        return this.http.get<IPagedOperationModel>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations`, {params})
            .pipe(
                map((response: IPagedOperationModel) => {
                    //revert back to zero based paging
                    response.metadata.pageNumber--;

                    // parse the operationJson field to parsedOperationJson
                    this.parseOperationRecords(response.records);

                    return response;
                }),
                catchError((error: HttpErrorResponse) => {
                    return this.errorHandler.handleError(error);
                })
            );
    }

    private parseOperationRecords(records: any[]): void {
        records.forEach(record => {
            try {
                const parsedJson = JSON.parse(record.operationJson);
                switch (record.operationType) {
                    case OperationType.CopyProperty:
                        record.parsedOperationJson = parsedJson as CopyPropertyOperation;
                        break;
                    case OperationType.ConditionalTransform:
                        record.parsedOperationJson = parsedJson as ConditionalTransformOperation;
                        break;
                    case OperationType.CodeMap:
                        record.parsedOperationJson = parsedJson as CodeMapOperation;
                        break;
                    case OperationType.CopyLocation:
                        record.parsedOperationJson = parsedJson as IOperation;
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
    }

    testOperation(operation: IOperationModel): Observable<IEntityCreatedResponse> {
        return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/test`, operation)
            .pipe(
                tap(_ => console.log(`Request for configuration creation was sent.`)),
                map((response: IEntityCreatedResponse) => {
                    return response;
                }),
                catchError((error) => this.errorHandler.handleError(error, false))
            )
    }

    testExistingOperation(operationId: string, resource: any): Observable<any> {
        return this.http.post<any>(`${this.appConfigService.config?.baseApiUrl}/normalization/operations/${operationId}/test`, resource)
            .pipe(
                tap(_ => console.log(`Request for configuration creation was sent.`)),
                map((response: any) => {
                    return response;
                }),
                catchError((error) => this.errorHandler.handleError(error, false))
            )
    }

}
