import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import {UserProfile } from "../../models/user-pofile.model";
import { AppConfigService } from "../app-config.service";
import { UserProfileService } from "../user-profile.service";
import { join as pathJoin } from '@fireflysemantics/join';
import {OAuthService} from "angular-oauth2-oidc";
import {firstValueFrom} from "rxjs/internal/firstValueFrom";


@Injectable({
  providedIn: 'root'
})

export class AuthenticationService {
  userProfile!: UserProfile;

  constructor(private http: HttpClient, private profileService: UserProfileService, public appConfigService: AppConfigService, private oauthService: OAuthService) { }

  async loadProfile() {
    const response: UserProfile = await firstValueFrom(this.http.get<UserProfile>(`${this.appConfigService.config?.baseApiUrl}/user`, { withCredentials: true }));

    this.userProfile = new UserProfile(
      response.email,
      response.firstName,
      response.lastName,
      response.roles,
      response.permissions
    );
    this.profileService.setProfile(this.userProfile);
  }

  async login() {
    if (this.appConfigService.config?.oauth2?.enabled) {
      await this.oauthService.tryLogin();

      if (!this.oauthService.hasValidAccessToken()) {
        this.oauthService.initCodeFlow();
      }
    } else {
      window.location.href = pathJoin(this.appConfigService.config?.baseApiUrl || '/api', 'login');
    }
  }

  logout() {
    this.profileService.clearProfile();
    if (this.appConfigService.config?.oauth2?.enabled) {
      // this.oauthService.logOut();
    } else {
      window.location.href = pathJoin(this.appConfigService.config?.baseApiUrl || '/api', 'logout');
    }
  }
}
