import {AuthenticationService} from "./authentication.service";
import {CanActivate, Router} from "@angular/router";
import {Injectable} from "@angular/core";
import {UserProfileService} from "../user-profile.service";

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthenticationService,
    private router: Router,
    private userService: UserProfileService
  ) {}

  async canActivate(): Promise<boolean> {

    let storedProfile = await this.userService.getProfile();
    if(storedProfile?.email) return true;

    const profile = await this.authService.loadProfile(); // calls /api/info

    if (profile?.email)  return true;

    this.router.navigate(['/login']);

    return false;
  }
}
