import {Component, OnInit} from '@angular/core';
import {UserProfileService} from '../../../services/user-profile.service';
import {Router} from "@angular/router";

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent implements OnInit {

  private name: string | undefined;

  constructor(private userProfileService: UserProfileService, private router: Router) {
  }

  async ngOnInit(): Promise<void> {
    const returnUrl = sessionStorage.getItem('returnUrl');
    if (returnUrl) {
      sessionStorage.removeItem('returnUrl'); // prevent loops
      await this.router.navigateByUrl(returnUrl);
      return;
    }

    try {
      const profile = await this.userProfileService.getProfile();
      this.name = profile?.firstName || profile?.lastName ? `${profile.firstName ?? ''} ${profile.lastName ?? ''}`.trim() : '';
    } catch (error) {
      console.error('Failed to fetch user profile:', error);
    }
  }


  get myName() {
    return this.name;
  }
}
