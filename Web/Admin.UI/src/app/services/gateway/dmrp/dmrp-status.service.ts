import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { AppConfigService } from '../../app-config.service';
import { IDmrpStatus } from '../../../interfaces/dmrp/dmrp-status.interface';

/**
 * What the screens assume when Tenant cannot be asked: DMRP off. The form then asks for a schedule,
 * which Tenant refuses loudly if DMRP is in fact on, rather than quietly creating a facility that
 * reports nothing.
 */
export const DMRP_OFF: IDmrpStatus = { dmrpEnabled: false, mockDmrpEnabled: false };

/**
 * Reads DMRP's state from Tenant, so the UI keeps no copy of the DMRP:Enabled setting.
 *
 * The answer only changes when Tenant restarts, so one call serves the whole session. A failed call is
 * not kept: it reads as {@link DMRP_OFF} and the next caller asks again.
 */
@Injectable({
  providedIn: 'root'
})
export class DmrpStatusService {
  private status$?: Observable<IDmrpStatus>;

  constructor(private http: HttpClient, private appConfigService: AppConfigService) {
  }

  getStatus(): Observable<IDmrpStatus> {
    if (!this.status$) {
      this.status$ = this.http.get<IDmrpStatus>(`${this.appConfigService.config?.baseApiUrl}/dmrp/dmrp-status`)
        .pipe(
          map(status => ({
            dmrpEnabled: status?.dmrpEnabled === true,
            mockDmrpEnabled: status?.dmrpEnabled === true && status?.mockDmrpEnabled === true
          })),
          catchError(() => {
            this.status$ = undefined;
            return of(DMRP_OFF);
          }),
          shareReplay(1)
        );
    }

    return this.status$;
  }
}
