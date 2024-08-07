import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs/internal/firstValueFrom';
import { UserProfileService } from '../../../services/user-profile.service';
import { UserProfile } from '../../../models/user-pofile.model';

@Component({
  selector: 'demo-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent {

  private name: string = "";
  constructor(private http: HttpClient, private userProfileService: UserProfileService) {
  }

  async ngOnInit(): Promise<void> {

    let result: Array<any> = await firstValueFrom(this.http.get<Array<any>>('/api/user'));
    console.log('got result:', result);
    let firstName: string = "";
    let lastName: string = "";
    let email: string = "";
    result.forEach((entry: any) => {
      if (entry?.type == "given_name") {
        firstName = entry.value;
      }
      if (entry?.type == "family_name") {
        lastName= entry.value;
      }
      if (entry?.type == "email") {
        email = entry.value;
      }
    });
    let profile = new UserProfile(email, firstName, lastName, [''], [''], ['']);
    this.name = profile.firstName + ' ' + profile.lastName;
    this.userProfileService.setProfile(profile);
  }

  get myName() {
    return this.name;
  }
}
