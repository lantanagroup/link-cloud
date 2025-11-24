import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthenticationService } from '../../services/security/authentication.service';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import {AppConfigService} from "../../services/app-config.service";

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule
  ],
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {

  loading = false;   // show spinner while logging in
  error = false;
  loginRequired = true;


  constructor(
    private authService: AuthenticationService,
    public appConfigService: AppConfigService,
    private router: Router
  ) {}

  ngOnInit() {
    this.login();
  }

  async login() {
    this.loading = true;
    this.error = false;
    this.loginRequired = this.appConfigService.config?.authRequired || false;

    try {
      if (this.loginRequired) {
        await this.authService.login();
      }
      else{
        await this.router.navigate(['/dashboard']);
      }
    } catch (error) {
      console.error('Login failed', error);
      this.error = true;
    } finally {
      this.loading = false; // stop spinner
    }
  }

}
