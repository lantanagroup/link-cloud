import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {ErrorHandlingService} from '../../error-handling.service';
import {Observable, tap, map, catchError, throwError} from 'rxjs';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {IEntityDeletedResponse} from 'src/app/interfaces/entity-deleted-response.interface';
import {
  IDataAcquisitionQueryConfigModel
} from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface';
import {
  IDataAcquisitionFhirListConfigModel
} from 'src/app/interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import {
  ICreateSftpConfigurationModel,
  ISftpConfigurationModel,
  ISftpCredentialsModel,
  ISftpCredentialStatusModel,
  ISftpConnectionTestResult
} from 'src/app/interfaces/data-acquisition/sftp-config-model.interface';
import {
  IDataAcquisitionAuthenticationConfigModel
} from '../../../interfaces/data-acquisition/data-acquisition-auth-config-model.interface';
import {AppConfigService} from '../../app-config.service';
import {IQueryPlanModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";
import {
  IOrganizationLocationConfigurationModel,
  ICreateOrganizationLocationConfigurationModel,
  IUpdateOrganizationLocationConfigurationModel
} from 'src/app/interfaces/data-acquisition/organization-location-config-model.interface';
import {IFhirPathValidationResponse} from 'src/app/interfaces/data-acquisition/fhir-path-validation-response.interface';

@Injectable({
  providedIn: 'root'
})
export class DataAcquisitionService {
  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) {
  }

  getFhirQueryConfiguration(facilityId: string): Observable<IDataAcquisitionQueryConfigModel> {
    return this.http.get<IDataAcquisitionQueryConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryConfiguration`)
      .pipe(
        tap(_ => console.log(`Fetched FHIR query configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  createFhirQueryConfiguration(facilityId: string, fhirQueryConfig: IDataAcquisitionQueryConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryConfiguration`, fhirQueryConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  updateFhirQueryConfiguration(facilityId: string, fhirQueryConfig: IDataAcquisitionQueryConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryConfiguration`, fhirQueryConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteFhirQueryConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryConfiguration`)
      .pipe(
        tap(_ => console.log(`Request for FHIR query configuration deletion was sent.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  getFhirListConfiguration(facilityId: string): Observable<IDataAcquisitionFhirListConfigModel> {
    return this.http.get<IDataAcquisitionFhirListConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryList`)
      .pipe(
        tap(_ => console.log(`Fetched FHIR list configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  getQueryPlanConfiguration(facilityId: string, type: string): Observable<IQueryPlanModel> {
    return this.http.get<IQueryPlanModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan?type=${type}`)
      .pipe(
        tap(_ => console.log(`Fetched Query Plan configuration.`)),
        map((response: IQueryPlanModel) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }


  createQueryPlanConfiguration(facilityId: string, queryPlan: IQueryPlanModel): Observable<IQueryPlanModel> {
    return this.http.post<IQueryPlanModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan`, queryPlan)
      .pipe(
        tap(_ => console.log(`Post Query Plan configuration.`)),
        map((response) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  updateQueryPlanConfiguration(facilityId: string, queryPlan: IQueryPlanModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan`, queryPlan)
      .pipe(
        tap(_ => console.log(`Update Query Plan configuration.`)),
        map((response) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteQueryPlanConfiguration(facilityId: string, type: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan?type=${type}`)
      .pipe(
        tap(_ => console.log(`Delete Query Plan configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteAllQueryPlanConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/QueryPlan/All`)
      .pipe(
        tap(_ => console.log(`Delete All Query Plan configuration.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  createFhirListConfiguration(facilityId: string, fhirListConfig: IDataAcquisitionFhirListConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryList`, fhirListConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  //NOTE: currently no PUT endpoint for fhir list. Commenting this out for now.
  updateFhirListConfiguration(facilityId: string, fhirListConfig: IDataAcquisitionFhirListConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/fhirQueryList`, fhirListConfig)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteFhirListConfiguration(facilityId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/fhirQueryList`)
      .pipe(
        tap(_ => console.log(`Request for FHIR list configuration deletion was sent.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  getAuthenticationConfig(facilityId: string, queryConfigType: string) {
    return this.http.get<IDataAcquisitionAuthenticationConfigModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`)
      .pipe(
        tap(_ => console.log(`Fetched authentication configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  createAuthenticationConfig(facilityId: string, queryConfigType: string, authenticationConfig: IDataAcquisitionAuthenticationConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`, authenticationConfig)
      .pipe(
        tap(_ => console.log(`Request for authentication configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  updateAuthenticationConfig(facilityId: string, queryConfigType: string, authenticationConfig: IDataAcquisitionAuthenticationConfigModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/${queryConfigType}/authentication`, authenticationConfig)
      .pipe(
        tap(_ => console.log(`Request for authentication configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  // SFTP Configuration Methods

  getSftpConfiguration(facilityId: string): Observable<ISftpConfigurationModel> {
    return this.http.get<ISftpConfigurationModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations`)
      .pipe(
        tap(_ => console.log(`Fetched SFTP configuration.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  createSftpConfiguration(facilityId: string, config: ICreateSftpConfigurationModel): Observable<IEntityCreatedResponse> {
    return this.http.post<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations`, config)
      .pipe(
        tap(_ => console.log(`Request for SFTP configuration creation was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  updateSftpConfiguration(facilityId: string, configId: string, config: ISftpConfigurationModel): Observable<IEntityCreatedResponse> {
    return this.http.put<IEntityCreatedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/${configId}`, config)
      .pipe(
        tap(_ => console.log(`Request for SFTP configuration update was sent.`)),
        map((response: IEntityCreatedResponse) => {
          return response;
        }),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteSftpConfiguration(facilityId: string, configId: string): Observable<IEntityDeletedResponse> {
    return this.http.delete<IEntityDeletedResponse>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/${configId}`)
      .pipe(
        tap(_ => console.log(`Request for SFTP configuration deletion was sent.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  // SFTP Credentials Methods

  getSftpCredentialStatus(facilityId: string): Observable<ISftpCredentialStatusModel> {
    return this.http.get<ISftpCredentialStatusModel>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/credentials/status`)
      .pipe(
        tap(_ => console.log(`Fetched SFTP credential status.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  updateSftpCredentials(facilityId: string, credentials: ISftpCredentialsModel): Observable<void> {
    return this.http.put<void>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/credentials`, credentials)
      .pipe(
        tap(_ => console.log(`Request for SFTP credentials update was sent.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  deleteSftpCredentials(facilityId: string): Observable<void> {
    return this.http.delete<void>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/credentials`)
      .pipe(
        tap(_ => console.log(`Request for SFTP credentials deletion was sent.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  testSftpConnection(facilityId: string): Observable<ISftpConnectionTestResult> {
    return this.http.post<ISftpConnectionTestResult>(`${this.appConfigService.config?.baseApiUrl}/data/${facilityId}/sftp-configurations/test-connection`, {})
      .pipe(
        tap(_ => console.log(`SFTP connection test request was sent.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error);
        })
      )
  }

  // Reporting Organization (Location Config) Methods

  getLocationConfigurations(facilityId: string): Observable<IOrganizationLocationConfigurationModel[]> {
    return this.http.get<IOrganizationLocationConfigurationModel[]>(`${this.appConfigService.config?.baseApiUrl}/data/location-config/facility/${facilityId}`)
      .pipe(
        tap(_ => console.log(`Fetched location configurations.`)),
        catchError((error) => {
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  createLocationConfiguration(facilityId: string, config: ICreateOrganizationLocationConfigurationModel): Observable<IOrganizationLocationConfigurationModel> {
    return this.http.post<IOrganizationLocationConfigurationModel>(`${this.appConfigService.config?.baseApiUrl}/data/location-config/facility/${facilityId}`, config)
      .pipe(
        tap(_ => console.log(`Request for location configuration creation was sent.`)),
        map((response: IOrganizationLocationConfigurationModel) => {
          return response;
        }),
        catchError((error) => {
          // Suppress the global bottom toast; the dialog shows the error inline.
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  updateLocationConfiguration(configId: number, config: IUpdateOrganizationLocationConfigurationModel): Observable<IOrganizationLocationConfigurationModel> {
    return this.http.put<IOrganizationLocationConfigurationModel>(`${this.appConfigService.config?.baseApiUrl}/data/location-config/${configId}`, config)
      .pipe(
        tap(_ => console.log(`Request for location configuration update was sent.`)),
        map((response: IOrganizationLocationConfigurationModel) => {
          return response;
        }),
        catchError((error) => {
          // Suppress the global bottom toast; the dialog shows the error inline.
          return this.errorHandler.handleError(error, false);
        })
      )
  }

  deleteLocationConfiguration(configId: number): Observable<any> {
    return this.http.delete<any>(`${this.appConfigService.config?.baseApiUrl}/data/location-config/${configId}`)
      .pipe(
        tap(_ => console.log(`Request for location configuration deletion was sent.`)),
        catchError((error) => {
          throw error;
        })
      )
  }

  // Validate a FHIRPath expression for a resource type via the MeasureEval $validate endpoint.
  validateFhirPath(resourceType: string, fhirPath: string): Observable<IFhirPathValidationResponse> {
    return this.http.post<IFhirPathValidationResponse>(
      `${this.appConfigService.config?.baseApiUrl}/measureeval/fhir-path/$validate`,
      { resourceType, fhirPath }
    )
      .pipe(
        tap(_ => console.log(`FHIRPath validation request was sent.`)),
        catchError((error) => this.errorHandler.handleError(error, false))
      );
  }

}
