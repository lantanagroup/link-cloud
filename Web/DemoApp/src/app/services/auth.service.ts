import { Injectable } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { UserProfileService } from './user-profile.service';
import { filter } from 'rxjs';
import { UserProfile } from '../models/user-pofile.model';
import { AppConfigService } from './app-config.service';


@Injectable({
  providedIn: 'root'
})
export class AuthService {
  userProfile!: UserProfile;
  

  constructor(private oauthService: OAuthService, private profileService: UserProfileService, appConfigService: AppConfigService) {

    if (!this.oauthService.hasValidAccessToken()) {
      this.oauthService.configure(appConfigService.getAuthCodeFlowConfig());
      this.oauthService.loadDiscoveryDocumentAndLogin();
      this.oauthService.setupAutomaticSilentRefresh();

      // Automatically load user profile
      this.oauthService.events
        .pipe(filter((e) => e.type === 'token_received'))
        .subscribe((_) => {
          this.oauthService.loadUserProfile().then(() => {
            //build user profile from token          
            const claims = this.oauthService.getIdentityClaims();
            //const scopes = this.oauthService.getGrantedScopes();

            this.userProfile = new UserProfile(
              claims['email'],
              claims['given_name'],
              claims['family_name'],
              [],
              ["Admin"]
            );

            this.profileService.setProfile(this.userProfile);            
          })          
        });
    }
    else {
      this.profileService.getProfile().then(
        profile => {
          this.userProfile = profile;
        }
      )   
    }

  }  

  isLoggedIn(): boolean {
    return this.oauthService.hasValidAccessToken();
  }

  logout(): void {
    this.oauthService.logOut();
  }
}
