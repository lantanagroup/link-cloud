import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserProfileService } from '../../services/user-profile.service';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'logout',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './logout.component.html'
})
export class LogOutComponent {

  constructor(private http: HttpClient, private userProfileService: UserProfileService) {

  }

  async ngOnInit(): Promise<void> {
    this.userProfileService.clearProfile();
  }
}
