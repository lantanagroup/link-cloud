import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Router } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faEllipsisV, faEdit } from '@fortawesome/free-solid-svg-icons';
import { ClickOutsideDirective } from 'src/app/directives/click-outside.directive';

@Component({
  selector: 'app-global-operations-table-command',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    ClickOutsideDirective
  ],
  templateUrl: './global-operations-table-command.component.html',
  styleUrl: './global-operations-table-command.component.scss'
})
export class GlobalOperationsTableCommandComponent {
  @Input() facilityId!: string;

  faEllipsisV = faEllipsisV;
  faEdit = faEdit;
  isOpen = false;

  constructor(private router: Router) {}

  toggleMenu() {
    this.isOpen = !this.isOpen;
  }

  navToFacilityConfig() {
    this.isOpen = false;
    if (this.facilityId) {
      this.router.navigate(['/tenant/facility', this.facilityId, 'edit']);
    }
  }
}