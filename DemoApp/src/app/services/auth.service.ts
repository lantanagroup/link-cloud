import { Injectable } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { UserProfileService } from './user-profile.service';
import { authCodeFlowConfig } from '../config/auth-code-flow.config';
import { filter } from 'rxjs';
import { UserProfile } from '../models/user-pofile.model';


@Injectable({
  providedIn: 'root'
})
export class AuthService {
  userProfile!: UserProfile;
  

  constructor(private oauthService: OAuthService, private profileService: UserProfileService) {

    if (!this.oauthService.hasValidAccessToken()) {
      this.oauthService.configure(authCodeFlowConfig);
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
              ["TestFacility01", "TestFacility02"],
              [],
              ["Admin"]
            );

            this.profileService.setProfile(this.userProfile);            
          })          
        });
    }
    else {
      this.userProfile = this.profileService.getProfile();      
    }

  }  

  isLoggedIn(): boolean {
    return this.oauthService.hasValidAccessToken();
  }

  logout(): void {
    this.oauthService.logOut();
  }
}
