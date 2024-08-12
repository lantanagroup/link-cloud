import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { UserClaims, UserProfile } from "../../models/user-pofile.model";
import { AppConfigService } from "../app-config.service";
import { ErrorHandlingService } from "../error-handling.service";
import { UserProfileService } from "../user-profile.service";
import { join as pathJoin } from '@fireflysemantics/join';

@Injectable({
  providedIn: 'root'
})

export class AuthenticationService {
  userProfile!: UserProfile;



  constructor(private http: HttpClient, private profileService: UserProfileService, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  loadUser() {
    this.http.get<UserProfile>(`${this.appConfigService.config?.baseApiUrl}/user`, { withCredentials: true })
      .subscribe((response: UserProfile) => {
        this.userProfile = new UserProfile(
          response.email,
          response.firstName,
          response.lastName,
          response.roles,
          response.permissions
        );
        this.profileService.setProfile(this.userProfile);
      });
  }

  login() {
    window.location.href = pathJoin(this.appConfigService.config?.baseApiUrl || '/api', 'login');
  }

  logout() {
    window.location.href = pathJoin(this.appConfigService.config?.baseApiUrl || '/api', 'logout');
  }

}
