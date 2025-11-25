import {AuthenticationService} from "./authentication.service";
import {ActivatedRouteSnapshot, CanActivate, Router, RouterStateSnapshot} from "@angular/router";
import {Injectable} from "@angular/core";
import {UserProfileService} from "../user-profile.service";

@Injectable({providedIn: 'root'})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthenticationService,
    private router: Router,
    private userService: UserProfileService
  ) {
  }

  async canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Promise<boolean> {

    if (state.url === '/login') {
      return true;
    }

    let storedProfile = await this.userService.getProfile();
    if (storedProfile?.email) return true;

    try {
      const profile = await this.authService.loadProfile();
      if (profile?.email) return true;
    } catch (error: any) {
      if (error.status !== 401) {
        return false;
      }
    }
    sessionStorage.setItem('returnUrl', state.url);
    this.router.navigate(['/login']);
    return false;
  }
}
