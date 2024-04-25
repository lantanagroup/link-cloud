import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { UserClaims, UserProfile } from "../../models/user-pofile.model";
import { AppConfigService } from "../app-config.service";
import { ErrorHandlingService } from "../error-handling.service";
import { UserProfileService } from "../user-profile.service";

@Injectable({
  providedIn: 'root'
})

export class AuthenticationService {
  userProfile!: UserProfile;

  private baseURL = "/api/login"


  constructor(private http: HttpClient, private profileService: UserProfileService, private errorHandler: ErrorHandlingService, public appConfigService: AppConfigService) { }

  loadUser() {    
    this.http.get<UserClaims[]>(`${this.appConfigService.config?.baseApiUrl}/user`, { withCredentials: true })
      .subscribe((response: UserClaims[]) => {
        this.userProfile = new UserProfile(
          response.find(x => x.type === 'email')?.value || '',
          response.find(x => x.type === 'given_name')?.value || '',
          response.find(x => x.type === 'family_name')?.value || '',
          response.filter(x => x.type === 'facilities').map(y => y.value),
          [],
          response.filter(x => x.type === 'roles').map(y => y.value)
        );
        this.profileService.setProfile(this.userProfile);
      });    
  }

  login() {
    return this.http.get(`${this.baseURL}`)
      .subscribe((response) => {
        console.log(response);
      });
  }
  
  logout() {
    return this.http.get(`${this.appConfigService.config?.baseApiUrl}/logout`)
      .subscribe(_ => {
        this.profileService.clearProfile();
      });
  }

}
