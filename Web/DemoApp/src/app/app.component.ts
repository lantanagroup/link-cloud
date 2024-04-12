import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, OnInit } from '@angular/core';
import { map, Observable, shareReplay } from 'rxjs';
import { UserProfile } from './models/user-pofile.model';
import { AuthService } from './services/auth.service';
import { UserProfileService } from './services/user-profile.service';


@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
})
export class AppComponent implements OnInit {
  title = 'DemoApp';
  userProfile: UserProfile | undefined;
  showMenuText: boolean = true;

  isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  constructor(private breakpointObserver: BreakpointObserver, private authService: AuthService, private profileService: UserProfileService) {

    this.profileService.userProfileUpdated.subscribe(profile => {
      this.userProfile = profile;
    });    

  }

  ngOnInit(): void {
    this.userProfile = this.profileService.getProfile(); 
  }

  logout() {
    this.authService.logout();
  }

  toggleMenuText() {
    this.showMenuText = !this.showMenuText;
  }

}
