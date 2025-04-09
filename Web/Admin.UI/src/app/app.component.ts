import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, OnChanges, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, shareReplay } from 'rxjs';
import { UserProfile } from './models/user-pofile.model';
import { AppConfigService } from './services/app-config.service';
import { AuthenticationService } from './services/security/authentication.service';
import { UserProfileService } from './services/user-profile.service';


@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    standalone: false
})
export class AppComponent implements OnInit, OnChanges {
  userProfile: UserProfile | undefined;
  showMenuText: boolean = true;
  loginRequired = true;

  isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  constructor(private breakpointObserver: BreakpointObserver, private authService: AuthenticationService, private profileService: UserProfileService, public appConfigService: AppConfigService, private router: Router) {

    this.profileService.userProfileUpdated.subscribe(profile => {
      this.userProfile = profile;
    });

  }

  async ngOnChanges(): Promise<void> {
    this.userProfile = await this.profileService.getProfile();
  }

  async ngOnInit(): Promise<void>{
    this.loginRequired = this.appConfigService.config?.authRequired || false;
    this.userProfile = await this.profileService.getProfile();

    if (this.userProfile.email === '' && this.loginRequired) {
      this.authService.login();
    }

    this.profileService.getProfileEvent().subscribe(profile => {
      this.userProfile = profile;
    });

  }

  logout() {
    if (this.loginRequired) {
      this.authService.logout();
    }
  }
}
