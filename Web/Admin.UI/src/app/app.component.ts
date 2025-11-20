import {BreakpointObserver, Breakpoints} from '@angular/cdk/layout';
import {Component, OnChanges, OnDestroy, OnInit} from '@angular/core';
import {Subscription} from 'rxjs';
import {UserProfile} from './models/user-pofile.model';
import {AppConfigService} from './services/app-config.service';
import {AuthenticationService} from './services/security/authentication.service';
import {UserProfileService} from './services/user-profile.service';
import {ActivatedRoute, NavigationEnd, Router} from "@angular/router";


@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: false
})
export class AppComponent implements OnInit, OnChanges, OnDestroy {

  userProfile: UserProfile | undefined;
  loginRequired = true;
  private profileSubscription?: Subscription;

  constructor(private router: Router, private route: ActivatedRoute, private breakpointObserver: BreakpointObserver, private authService: AuthenticationService, private profileService: UserProfileService, public appConfigService: AppConfigService) {

  }

  async ngOnChanges(): Promise<void> {
    this.userProfile = await this.profileService.getProfile();
  }

  async ngOnInit(): Promise<void> {
    this.loginRequired = this.appConfigService.config?.authRequired || false;

    this.profileSubscription = this.profileService.userProfileUpdated.subscribe(profile => {
      this.userProfile = profile;
    });

    // Initialize profile from session storage if user refreshed the page
    this.userProfile = await this.profileService.getProfile();

  }

  showDashboard(){
    return (!!this.userProfile?.email) || !this.loginRequired;
  }

  logout() {
    if (this.loginRequired) {
      this.authService.logout();
      this.router.navigate(['/logout']);
    }
  }

  ngOnDestroy(): void {
    this.profileSubscription?.unsubscribe();
  }

}
