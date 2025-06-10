import { NgFor, NgIf } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { Component } from '@angular/core';
import { VdIconComponent } from "../vd-icon/vd-icon.component";

export interface SubnavItem {
  label: string;
  path?: string; // For routerLink
  children?: SubnavItem[]; // For dropdowns
}

@Component({
  selector: 'app-link-admin-subnav-bar',
  imports: [
    RouterLink,
    RouterLinkActive,
    VdIconComponent,
    NgFor,
    NgIf
  ],
  templateUrl: './link-admin-subnav-bar.component.html',
  styleUrls: ['./link-admin-subnav-bar.component.scss']
})
export class LinkAdminSubnavBarComponent {
  subnavItems: SubnavItem[] = [
    { label: 'Dashboard', path: '#' },
    { label: 'Submissions', path: '/sub-pre-qual-report', },
    { label: 'Logs', path: '#' },
    {
      label: 'Configurations',
      children: [
        { label: 'Summary', path: '#' },
        { label: 'Facilities', path: '#' },
        { label: 'Measures', path: '#' },
        { label: 'Query Plans', path: '#' },
        { label: 'Validation Categories', path: '#' }
      ]
    }
  ];
}