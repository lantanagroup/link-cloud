import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs/internal/firstValueFrom';
import { UserProfileService } from '../../../services/user-profile.service';
import { UserProfile } from '../../../models/user-pofile.model';
import {AppConfigService} from "../../../services/app-config.service";

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent {

  private name: string = "";
  constructor(private http: HttpClient, private userProfileService: UserProfileService, private appConfigService: AppConfigService) {
  }

  async ngOnInit(): Promise<void> {
    const baseApiUrl = this.appConfigService.config?.baseApiUrl || '/api';
    let result: UserProfile = await firstValueFrom(this.http.get<UserProfile>(`${baseApiUrl}/user`));
    console.log('got result:', result);

    let profile = new UserProfile(result.email, result.firstName, result.lastName, result.roles, result.permissions);
    this.name = profile.firstName + ' ' + profile.lastName;
    this.userProfileService.setProfile(profile);
  }

  get myName() {
    return this.name;
  }
}
