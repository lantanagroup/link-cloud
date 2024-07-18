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
    let profile = new UserProfile(result[4]?.value || "", result[2]?.value || "", result[1]?.value || "", result[5]?.value || [''], [''], ['']);
    this.name = profile.firstName + ' ' + profile.lastName;
    this.userProfileService.setProfile(profile);
  }

  get myName() {
    return this.name;
  }
}
