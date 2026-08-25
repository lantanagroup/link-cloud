import {CanActivate, Router} from "@angular/router";
import {Injectable} from "@angular/core";
import {AppConfigService} from "../app-config.service";

/**
 * Blocks the DMRP screens when the module is off (the app config's dmrpEnabled flag,
 * mirroring the backend's DMRP:Enabled). The nav bar already hides the entries; this
 * covers direct URLs and stale bookmarks.
 */
@Injectable({providedIn: 'root'})
export class DmrpGuard implements CanActivate {
  constructor(private router: Router, private appConfigService: AppConfigService) {
  }

  canActivate(): boolean {
    if (this.appConfigService.config?.dmrpEnabled) {
      return true;
    }

    this.router.navigate(['/dashboard']);
    return false;
  }
}
