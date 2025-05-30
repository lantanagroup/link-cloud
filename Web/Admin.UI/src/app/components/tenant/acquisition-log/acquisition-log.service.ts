import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable } from 'rxjs';
import { AppConfigService } from 'src/app/services/app-config.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { AcquisitionLogSummary } from './models/acquisition-log-summary';
import { AcquisitionLog } from './models/acquisition-log';

@Injectable({
  providedIn: 'root'
})
export class AcquisitionLogService {

  constructor(private http: HttpClient, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  baseUrl = `${this.appConfigService.config?.baseApiUrl}/data/acquisition`;

  getAcquisitionLogs(
    patientId: string | null,
    facility: string | null,
    reportId: string | null,
    resourceType: string | null,
    resourceId: string | null,
    queryType: string | null,
    queryPhase: string | null,
    status: string | null,
    priority: string | null,
    pageNumber: number,
    pageSize: number,
    showLoadingIndicator: boolean = true) : Observable<AcquisitionLogSummary[]> {

    const headers = new HttpHeaders({ 'X-Skip-Loading': 'true' });

    //java based paging is zero based, so increment page number by 1
    pageNumber = pageNumber + 1;

    let queryString: string = `pageNumber=${pageNumber}&pageSize=${pageSize}`;

    //add filters to query string
    if(patientId) {
        queryString += `&patient=${encodeURIComponent(patientId)}`;
    }
    if(facility) {
        queryString += `&facility=${encodeURIComponent(facility)}`;
    }
    if(reportId) {
        queryString += `&reportId=${encodeURIComponent(reportId)}`;
    }
    if(resourceType) {
        queryString += `&resourceType=${encodeURIComponent(resourceType)}`;
    }
    if(resourceId) {
        queryString += `&resourceId=${encodeURIComponent(resourceId)}`;
    }
    if(queryType) {
        queryString += `&queryType=${encodeURIComponent(queryType)}`;
    }
    if(queryPhase) {
        queryString += `&queryPhase=${encodeURIComponent(queryPhase)}`;
    }
    if(status) {
        queryString += `&status=${encodeURIComponent(status)}`;
    }
    if(priority) {
        queryString += `&priority=${encodeURIComponent(priority)}`;
    }

    //for now, just return test data
    let acquisitionLogs = [
      {
        id: '1',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Patient'],
        resourceId: '12345',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Read',
        scheduledDate: new Date(),
        status: 'Completed',
        reportIds: ['report-001']

      },
      {
        id: '2',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Encounter'],
        resourceId: '',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Search',
        scheduledDate: new Date(),
        status: 'Pending',
        reportIds: ['report-001']
      },
      {
        id: '3',
        priority: 'Normal',
        patientId: '78954',
        facilityId: 'TestFacility2',
        resourceTypes: ['Patient'],
        resourceId: '78954',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Read',
        scheduledDate: new Date(),
        status: 'Completed',
        reportIds: ['2327f739-4e60-4f2d-81e1-662d5fe8a2de']

      },
      {
        id: '4',
        priority: 'Normal',
        patientId: '78954',
        facilityId: 'TestFacility2',
        resourceTypes: ['Encounter'],
        resourceId: '',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Search',
        scheduledDate: new Date(),
        status: 'Processing',
        reportIds: ['2327f739-4e60-4f2d-81e1-662d5fe8a2de']
      },
      {
        id: '5',
        priority: 'Normal',
        patientId: '78954',
        facilityId: 'TestFacility2',
        resourceTypes: ['Location'],
        resourceId: '',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Search',
        scheduledDate: new Date(),
        status: 'Pending',
        reportIds: ['2327f739-4e60-4f2d-81e1-662d5fe8a2de']
      }
    ];

    if(reportId) {
      acquisitionLogs = acquisitionLogs.filter(log => log.reportIds.includes(reportId));
    }

    return new Observable<AcquisitionLogSummary[]>(observer => {
      observer.next(acquisitionLogs);
      observer.complete();
    });

    if(showLoadingIndicator)
    {
      return this.http.get<AcquisitionLogSummary[]>(`${this.baseUrl}/acquisition-logs?${queryString}`)
      .pipe(
        map((response: AcquisitionLogSummary[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
            var err = this.errorHandler.handleError(error);
            return err;
        })
      )
    }
    else
    {
      return this.http.get<AcquisitionLogSummary[]>(`${this.baseUrl}/acquisition-logs?${queryString}`, { headers: headers })
      .pipe(
        map((response: AcquisitionLogSummary[]) => {
          return response;
        }),
        catchError((error: HttpErrorResponse) => {
            var err = this.errorHandler.handleError(error);
            return err;
        })
      )
    }
  }

  getAcquisitionLog(id: string) : Observable<AcquisitionLog> {

    //temporary test data
    let acquisitionLog: AcquisitionLog = {
      id: 'log-001',
      priority: 'Normal',
      facilityId: 'Facility-123',
      patientId: 'Patient-456',
      fhirVersion: 'R4',
      queryType: 'Search',
      queryPhase: 'Initial',
      status: 'Completed',
      executionDate: new Date('2025-05-10T10:00:00Z'),
      timeZone: 'America/New_York',
      retryAttempts: 0,
      completionDate: new Date('2025-05-10T10:00:30Z'),
      completionTimeMilliseconds: 30000,
      resourcesAcquired: [
        'Encounter/enc-789',
        'Encounter/enc-790',
        'Encounter/enc-791',
        'Encounter/enc-792',
        'Encounter/enc-793'
      ],
      referencedResources: [
        {
          queryPhase: 'Initial',
          identifier: 'Location/loc-789',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Location/loc-656',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Location/loc-657',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Condition/con-456',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Condition/con-457',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Condition/con-458',
        },
        {
          queryPhase: 'Initial',
          identifier: 'Encounter/enc-790',
        }
      ],
      notes: ['Query executed successfully.'],
      fhirQuery: {
        QueryType: 'Search',
        resourceTypes: ['Encounter'],
        queryParameters: ['status=finished'],
        query: 'Encounter?status=finished',
        referenceTypes: [
          {
            queryPhase: 'Initial',
            referenceType: 'Location',
          },
          {
            queryPhase: 'Initial',
            referenceType: 'Condition'
          }
        ],
        paged: 1,
      },
      scheduledReport: {
        reportId: 'report-001',
        measure: 'encounter-completion',
        startDate: new Date('2025-05-01'),
        endDate: new Date('2025-05-10'),
      },
    };

    return new Observable<AcquisitionLog>(observer => {
      observer.next(acquisitionLog);
      observer.complete();
    });


    return this.http.get<AcquisitionLog>(`${this.baseUrl}/${id}`)
    .pipe(
      map((response: AcquisitionLog) => {
        return response;
      }),
      catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
      })
    )
  }

  executeAcquisitionLog(id: string) : Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/process/${id}`, id)
    .pipe(
      map((response: any) => {
        return response;
      }),
      catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
      })
    )
  }

  getResourceTypes(): Observable<string[]> {

    //temporary test data
    let types = ['Patient', 'Encounter', 'Location', 'Observation', 'MedicationRequest', 'Procedure'];
    return new Observable<string[]>(observer => {

      observer.next(types);
      observer.complete();
    });

    return this.http.get<string[]>(`${this.baseUrl}/acquisition-logs/resource-types`)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          var err = this.errorHandler.handleError(error);
          return err;
        })
      );
  }
}
