import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
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

  constructor(private sessionStorageSrv: SessionStorageService) { }

  setProfile(profile: IUserProfile) {
    this.sessionStorageSrv.storeItem(this.profileKey, JSON.stringify(profile));
    this._userProfileUpdatedSubject.next(profile);
  }

  getProfile(): IUserProfile {
    let profile = this.sessionStorageSrv.getItem(this.profileKey);

    if (profile) {
      return JSON.parse(profile) as IUserProfile;
    }
    else {
      return new UserProfile('', '', '', [''], [''], ['']); 
    } 
  }

  clearProfile() {
    this.sessionStorageSrv.removeItem(this.profileKey);
  }

}
