import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterModule } from '@angular/router';

export interface NavItem {
  label: string;
  path?: string;
}

@Component({
  selector: 'app-link-admin-nav-bar',
  imports: [
    NgFor,
    RouterModule
  ],
  templateUrl: './link-admin-nav-bar.component.html',
  styleUrls: ['./link-admin-nav-bar.component.scss'],
  standalone: true,
})
export class LinkAdminNavBarComponent {
  readonly navItems: readonly NavItem[] = [
    { label: 'Profile', path: '#' },
    { label: 'System Admin', path: '#' },
    { label: 'Resources', path: '#' }
  ];
}
