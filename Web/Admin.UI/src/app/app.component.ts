import {BreakpointObserver, Breakpoints} from '@angular/cdk/layout';
import {Component, OnChanges, OnInit} from '@angular/core';
import {filter, map, Observable, shareReplay} from 'rxjs';
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
export class AppComponent implements OnInit, OnChanges {

  userProfile: UserProfile | undefined;
  loginRequired = true;

  isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  constructor(private router: Router, private route: ActivatedRoute, private breakpointObserver: BreakpointObserver, private authService: AuthenticationService, private profileService: UserProfileService, public appConfigService: AppConfigService) {

    this.profileService.userProfileUpdated.subscribe(profile => {
      this.userProfile = profile;

      // remove the logged flag
      const urlTree = this.router.parseUrl(this.router.url);
      const loggedFlag = urlTree.queryParams['logged'];

      if (profile.email && loggedFlag) {
        const updatedParams = {...urlTree.queryParams};
        delete updatedParams['logged'];

        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: updatedParams,
          replaceUrl: true,
          queryParamsHandling: ''
        });
      }
    });
  }

  async ngOnChanges(): Promise<void> {
    this.userProfile = await this.profileService.getProfile();
  }

  async ngOnInit(): Promise<void> {
    this.loginRequired = this.appConfigService.config?.authRequired || false;

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(async () => {
        const urlTree = this.router.parseUrl(this.router.url);
        if (['/logout', '/unauthorized'].includes(this.router.url)) {
          return; // do nothing on logout/unauthorized pages
        }

        this.userProfile = await this.profileService.getProfile();
        const loggedFlag = urlTree.queryParams['logged'];

        if (this.userProfile.email === '' && this.loginRequired) {
          if (!loggedFlag) {
            await this.authService.login();
            return;
          }
        }
      });

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
}
