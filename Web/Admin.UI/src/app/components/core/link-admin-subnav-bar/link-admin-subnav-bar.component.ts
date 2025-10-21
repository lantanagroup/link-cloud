import { IsActiveMatchOptions, Router, RouterLink, RouterLinkActive } from '@angular/router';


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
    VdIconComponent
],
  templateUrl: './link-admin-subnav-bar.component.html',
  styleUrls: ['./link-admin-subnav-bar.component.scss'],
  standalone: true,
})
export class LinkAdminSubnavBarComponent {
  constructor(private router: Router) { }

  subnavItems: SubnavItem[] = [
    { label: 'Dashboard' },
    { label: 'Submissions', path: '/sub-pre-qual-report', },
    { label: 'Logs' },
    {
      label: 'Configurations',
      children: [
        { label: 'Summary' },
        { label: 'Facilities' },
        { label: 'Measures' },
        { label: 'Query Plans' },
        { label: 'Validation Categories', path: '/validation-config/validation-categories' }
      ]
    }
  ];

  isChildRouteActive(children: SubnavItem[]): boolean {
    return children.some(child =>
      child.path && this.router.isActive(child.path, {
        paths: 'subset',
        queryParams: 'ignored',
        fragment: 'ignored',
        matrixParams: 'ignored'
      })
    );
  }
}