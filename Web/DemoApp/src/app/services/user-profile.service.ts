import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { IUserProfile } from '../interfaces/user-profile.interface';
import { UserProfile } from '../models/user-pofile.model';
import { SessionStorageService } from './session.service';

@Injectable({
  providedIn: 'root'
})
export class UserProfileService {
  private profileKey: string = "user-profile";
  private _userProfileUpdatedSubject = new Subject<UserProfile>();
  userProfileUpdated = this._userProfileUpdatedSubject.asObservable();

  constructor(private sessionStorageSrv: SessionStorageService, private http: HttpClient) { }

  setProfile(profile: IUserProfile) {
    this.sessionStorageSrv.storeItem(this.profileKey, JSON.stringify(profile));
    this._userProfileUpdatedSubject.next(profile);
  }

  getProfileEvent(): Observable<any> {
    return this.userProfileUpdated;
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
    console.log('clearing profile');
    this.sessionStorageSrv.removeItem(this.profileKey);
    console.log("profile is " + this.sessionStorageSrv.getItem(this.profileKey));
    this.sessionStorageSrv.clearSession();
    let profile = new UserProfile('', '', '', [''], [''])
    this._userProfileUpdatedSubject.next(profile);
  }

}
