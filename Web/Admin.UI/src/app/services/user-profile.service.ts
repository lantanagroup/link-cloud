import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Subject, firstValueFrom } from 'rxjs';
import { IUserProfile } from '../interfaces/user-profile.interface';
import { UserProfile } from '../models/user-pofile.model';
import { SessionStorageService } from './session.service';
import {AppConfigService} from "./app-config.service";

@Injectable({
  providedIn: 'root'
})
export class UserProfileService {
  private profileKey: string = "user-profile";
  private _userProfileUpdatedSubject = new Subject<UserProfile>();
  userProfileUpdated = this._userProfileUpdatedSubject.asObservable();

  constructor(private sessionStorageSrv: SessionStorageService, private http: HttpClient,  public appConfigService: AppConfigService) { }

  setProfile(profile: IUserProfile) {
    this.sessionStorageSrv.storeItem(this.profileKey, JSON.stringify(profile));
    this._userProfileUpdatedSubject.next(profile);
  }

  async getProfile(): Promise<IUserProfile> {
    let profile = this.sessionStorageSrv.getItem(this.profileKey);

    if (profile) {
      return JSON.parse(profile) as IUserProfile;
    }
    else {
      return new UserProfile('', '', '', [''], ['']);
    }
  }

  clearProfile() {
    this.sessionStorageSrv.removeItem(this.profileKey);
    this.sessionStorageSrv.clearSession();
    let profile = new UserProfile('', '', '', [''], [''])
    this._userProfileUpdatedSubject.next(profile);
  }


  async fetchAndSetProfile(): Promise<void> {
    try {
      const baseApiUrl = this.appConfigService.config?.baseApiUrl || '/api';
      const result = await firstValueFrom(this.http.get<UserProfile>(`${baseApiUrl}/user`));
      const profile = new UserProfile(result.email, result.firstName, result.lastName, result.roles, result.permissions);
      this.setProfile(profile);
    } catch (error) {
      throw error;
    }
  }
}
