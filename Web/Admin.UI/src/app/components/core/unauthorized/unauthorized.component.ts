import {Component, OnInit} from '@angular/core';
import {UserProfileService} from "../../../services/user-profile.service";

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [],
  templateUrl: './unauthorized.component.html',
  styleUrls: ['./unauthorized.component.scss']
})
export class UnauthorizedComponent implements OnInit {

  constructor(private userProfileService: UserProfileService) {

  }

  async ngOnInit(): Promise<void> {
    this.userProfileService.clearProfile();
    console.log('UnauthorizedComponent');
  }
}
