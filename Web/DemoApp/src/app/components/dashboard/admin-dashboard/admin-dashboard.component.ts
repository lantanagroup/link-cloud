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

  constructor(private http: HttpClient, private userProfileService: UserProfileService) {
  }

  async ngOnInit(): Promise<void> {
    let result : Array<any> = await firstValueFrom(this.http.get<Array<any>>('/api/user'));
    const profile = new UserProfile(result[4].value, result[2].value, result[3].value, result[5].value, [''], ['']); 
    console.log('got result:', result);
    this.userProfileService.setProfile(profile);
  }

}
