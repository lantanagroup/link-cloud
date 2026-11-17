import {CanActivate, Router, UrlTree} from "@angular/router";
import {Injectable} from "@angular/core";
import {Observable, map} from "rxjs";
import {DmrpStatusService} from "../gateway/dmrp/dmrp-status.service";

/**
 * Blocks the DMRP screens when the module is off, as Tenant reports it at api/dmrp/dmrp-status. The nav
 * bar already hides the entries; this covers direct URLs and stale bookmarks.
 */
@Injectable({providedIn: 'root'})
export class DmrpGuard implements CanActivate {
  constructor(private router: Router, private dmrpStatusService: DmrpStatusService) {
  }

  canActivate(): Observable<boolean | UrlTree> {
    return this.dmrpStatusService.getStatus().pipe(
      map(status => status.dmrpEnabled ? true : this.router.createUrlTree(['/dashboard']))
    );
  }
}
