import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {catchError, forkJoin, map, Observable, tap} from 'rxjs';
import {AppConfigService} from '../../app-config.service';
import {ErrorHandlingService} from '../../error-handling.service';
import {ITerminologyResource} from '../../../interfaces/terminology/terminology-resource.interface';

@Injectable({
  providedIn: 'root'
})
export class TerminologyService {
  constructor(
    private http: HttpClient,
    private appConfigService: AppConfigService,
    private errorHandler: ErrorHandlingService
  ) { }

  getResources(): Observable<ITerminologyResource[]> {
    const baseUrl = this.appConfigService.config?.baseApiUrl || '/api';
    const valueSets$ = this.http.get<any>(`${baseUrl}/terminology/fhir/ValueSet?_summary=true`);
    const codeSystems$ = this.http.get<any>(`${baseUrl}/terminology/fhir/CodeSystem?_summary=true`);

    return forkJoin([valueSets$, codeSystems$]).pipe(
      tap(_ => console.log('Fetched terminology resources')),
      map(([vsBundle, csBundle]) => {
        const resources: ITerminologyResource[] = [];

        if (vsBundle && vsBundle.entry) {
          vsBundle.entry.forEach((entry: any) => {
            if (entry.resource) {
              resources.push({
                resourceType: 'ValueSet',
                id: entry.resource.id,
                url: entry.resource.url,
                version: entry.resource.version || ''
              });
            }
          });
        }

        if (csBundle && csBundle.entry) {
          csBundle.entry.forEach((entry: any) => {
            if (entry.resource) {
              resources.push({
                resourceType: 'CodeSystem',
                id: entry.resource.id,
                url: entry.resource.url,
                version: entry.resource.version || ''
              });
            }
          });
        }

        return resources;
      }),
      catchError((error) => this.errorHandler.handleError(error))
    );
  }
}
