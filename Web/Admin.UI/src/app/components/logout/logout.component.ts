import {Component, OnInit} from '@angular/core';

import { UserProfileService } from '../../services/user-profile.service';
@Component({
  selector: 'logout',
  standalone: true,
  templateUrl: './logout.component.html'
})
export class LogOutComponent implements OnInit {

  constructor(private userProfileService: UserProfileService) {

  }

  async ngOnInit(): Promise<void> {
    this.userProfileService.clearProfile();
  }

}
