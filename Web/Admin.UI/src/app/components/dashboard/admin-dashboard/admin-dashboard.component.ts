import {Component, OnInit} from '@angular/core';
import { UserProfileService } from '../../../services/user-profile.service';


@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit {

  private name: string | undefined;
  constructor(private userProfileService: UserProfileService) {
  }

  async ngOnInit(): Promise<void> {
    try {
      await this.userProfileService.fetchAndSetProfile();
      const profile  = await this.userProfileService.getProfile();
      if (profile?.firstName && profile?.lastName) {
        this.name = profile.firstName + ' ' + profile.lastName;
      }
    }
    catch (error) {
      console.error('Failed to fetch user profile:', error);
    }
  }

  get myName() {
    return this.name;
  }
}
